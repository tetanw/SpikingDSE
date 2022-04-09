using System;
using System.Collections.Generic;

namespace SpikingDSE;


public sealed class ControllerV1 : Actor, ICore
{
    public Action<Actor, long, SpikeEvent> SpikeSent;
    public Action<Actor, long, SpikeEvent> SpikeReceived;
    public Action<Actor, long, int> TimeAdvanced;

    public InPort Input = new();
    public OutPort Output = new();

    private readonly object thisLoc;
    private readonly InputLayer inputLayer;
    private readonly ISpikeSource source;
    private MappingTable mapping;
    public ControllerV1Spec spec;

    public ControllerV1(InputLayer inputLayer, ISpikeSource source, object location, ControllerV1Spec spec)
    {
        this.inputLayer = inputLayer;
        this.source = source;
        this.thisLoc = location;
        this.spec = spec;
        this.Name = spec.Name;
    }

    public void LoadMapping(MappingTable mapping) => this.mapping = mapping;

    public object GetLocation() => thisLoc;

    public override IEnumerable<Event> Run(Simulator env)
    {
        env.Process(Sender(env));
        env.Process(Receiver(env));

        yield break;
    }

    private IEnumerable<Event> Sender(Simulator env)
    {
        int TS = 0;
        var destLayers = mapping.GetDestLayers(inputLayer);

        yield return env.SleepUntil(spec.StartTime);
        while (source.NextTimestep())
        {
            // Do spike sending
            var inputSpikes = source.NeuronSpikes();
            foreach (var neuron in inputSpikes)
            {
                foreach (var destLayer in destLayers)
                {
                    var spike = new SpikeEvent()
                    {
                        Layer = destLayer,
                        Neuron = neuron,
                        Feedback = false,
                        TS = TS,
                        CreatedAt = env.Now
                    };
                    var dest = mapping.CoordOf(spike.Layer);
                    var packet = new Packet
                    {
                        Src = thisLoc,
                        Dest = dest,
                        Message = spike
                    };
                    yield return env.Send(Output, packet);
                    SpikeSent?.Invoke(this, env.Now, spike);
                }
            }

            // Wait until sync
            foreach (var ev in Sync(env, TS))
                yield return ev;
            TS++;
        }

        foreach (var ev in Sync(env, TS))
            yield return ev;
    }

    private IEnumerable<Event> Sync(Simulator env, int TS)
    {
        var nextSync = spec.StartTime + spec.Interval * (TS + 1);
        yield return env.SleepUntil(Math.Max(nextSync, env.Now));

        var sync = new SyncEvent()
        {
            TS = TS,
            CreatedAt = env.Now
        };
        foreach (var core in mapping.Cores)
        {
            if (core is ControllerV1)
                continue;

            var flit = new Packet
            {
                Src = thisLoc,
                Dest = core.GetLocation(),
                Message = sync
            };
            yield return env.Send(Output, flit);
        }
        TimeAdvanced?.Invoke(this, env.Now, TS);
    }

    private IEnumerable<Event> Receiver(Simulator env)
    {
        while (true)
        {
            var rcv = env.Receive(Input);
            yield return rcv;
            var ev = rcv.Message;

            if (ev is Packet)
            {
                var spike = (ev as Packet).Message as SpikeEvent;
                SpikeReceived?.Invoke(this, env.Now, spike);
            }
            else
            {
                throw new Exception("Receiver received unknown message!");
            }
        }
    }

    string ICore.Name() => this.Name;

    OutPort ICore.Output() => Output;

    InPort ICore.Input() => Input;
}
