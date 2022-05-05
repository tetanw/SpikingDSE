using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;


public sealed class ControllerV1 : Actor, ICore
{
    public Action<Actor, long, SpikeEvent> SpikeSent;
    public Action<Actor, long, SpikeEvent> SpikeReceived;
    public Action<Actor, long, int> TimeAdvanced;

    public InPort Input = new();
    public OutPort Output = new();

    private readonly object location;
    private readonly InputLayer inputLayer;
    private readonly ISpikeSource source;
    private MappingTable mapping;
    public ControllerV1Spec spec;

    private Signal syncSignal;

    public ControllerV1(InputLayer inputLayer, ISpikeSource source, object location, ControllerV1Spec spec)
    {
        this.inputLayer = inputLayer;
        this.source = source;
        this.location = location;
        this.spec = spec;
        Name = spec.Name;
    }

    public void LoadMapping(MappingTable mapping) => this.mapping = mapping;

    public object GetLocation() => location;

    public override IEnumerable<Event> Run(Simulator env)
    {
        syncSignal = new(env);
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
                        Src = location,
                        Dest = dest,
                        Message = spike
                    };
                    yield return env.Send(Output, packet);
                    SpikeSent?.Invoke(this, env.Now, spike);
                }
            }

            // Wait until sync
            if (spec.GlobalSync)
            {
                var nextSync = spec.StartTime + spec.Interval * (TS + 1);
                yield return env.SleepUntil(Math.Max(nextSync, env.Now));

                // Sync
                foreach (var ev in Sync(env, TS))
                    yield return ev;
                TS++;
            }
            else
            {
                // Sync
                foreach (var ev in Sync(env, TS))
                    yield return ev;

                yield return syncSignal.Wait();
                TS++;
            }
        }

        if (!spec.GlobalSync)
        {
            // Sync
            foreach (var ev in Sync(env, TS))
                yield return ev;
        }
    }

    private IEnumerable<Event> Sync(Simulator env, int TS)
    {
        foreach (var core in mapping.Cores)
        {
            if (core is ControllerV1)
                continue;

            if (spec.IgnoreIdleCores && mapping.GetAllLayers(core).Count == 0)
                continue;

            var sync = new SyncEvent()
            {
                TS = TS,
                CreatedAt = env.Now,
                Layers = mapping.GetAllLayers(core) ?? new List<Layer>()
            };
            var flit = new Packet
            {
                Src = location,
                Dest = core.GetLocation(),
                Message = sync,
            };
            yield return env.Send(Output, flit);
        }
        TimeAdvanced?.Invoke(this, env.Now, TS);
    }

    private IEnumerable<Event> Receiver(Simulator env)
    {
        var coresDone = new HashSet<object>();
        int nrCores;
        if (spec.IgnoreIdleCores)
            nrCores = mapping.Cores.Where(c => mapping.GetAllLayers(c).Count > 0 && c is not ControllerV1).Count();
        else
            nrCores = mapping.Cores.Count - 1;

        while (true)
        {
            var rcv = env.Receive(Input);
            yield return rcv;
            var syncDone = (rcv.Message as Packet).Message as SyncDone;

            coresDone.Add(syncDone.Core);
            if (coresDone.Count == nrCores)
            {
                syncSignal.Notify();
                coresDone.Clear();
            }
        }
    }

    string ICore.Name() => Name;

    OutPort ICore.Output() => Output;

    InPort ICore.Input() => Input;

    public double Energy(long now)
    {
        return 0.0;
    }

    public double Memory() => 0.0;
}
