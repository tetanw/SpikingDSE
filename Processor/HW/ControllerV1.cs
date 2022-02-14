using System;
using System.Collections.Generic;

namespace SpikingDSE;


public sealed class ControllerV1 : Actor, Core
{
    public Action<Actor, long, SpikeEvent> SpikeSent;
    public Action<Actor, long, SpikeEvent> SpikeReceived;
    public Action<Actor, int> TimeAdvanced;

    public InPort Input = new();
    public OutPort Output = new();

    private object location;
    private InputLayer inputLayer;
    private long startTime;
    private long interval;
    private FIFO<object> outBuffer;
    private MappingTable mapping;

    public ControllerV1(InputLayer inputLayer, object location, int nrTimesteps, long startTime, long interval, string name = null)
    {
        this.inputLayer = inputLayer;
        this.location = location;
        this.startTime = startTime;
        this.interval = interval;
        this.Name = name;
    }

    public void LoadMapping(MappingTable mapping) => this.mapping = mapping;

    public object GetLocation() => location;

    public override IEnumerable<Event> Run(Simulator env)
    {
        outBuffer = new(env, 1);

        var timesteps = env.CreateResource(0);

        env.Process(SpikeSender(env, timesteps));
        env.Process(SyncSender(env, timesteps));

        env.Process(Sender(env));
        env.Process(Receiver(env));

        yield break;
    }

    private IEnumerable<Event> SpikeSender(Simulator env, Resource timesteps)
    {
        int TS = 0;
        while (inputLayer.spikeSource.NextTimestep())
        {
            // Send spikes for input layer
            var inputSpikes = inputLayer.spikeSource.NeuronSpikes();
            foreach (var neuron in inputSpikes)
            {
                foreach (var destLayer in mapping.GetDestLayers(inputLayer))
                {
                    var spike = new SpikeEvent()
                    {
                        Layer = destLayer,
                        Neuron = neuron,
                        Feedback = false,
                        TS = TS,
                        CreatedAt = env.Now
                    };
                    yield return outBuffer.RequestWrite();
                    outBuffer.Write(spike);
                    outBuffer.ReleaseWrite();
                    SpikeSent?.Invoke(this, env.Now, spike);
                }
            }

            // Wait until until the sync sender goes to next timestep
            yield return env.RequestResource(timesteps, 1);
            TS++;
        }
    }

    private IEnumerable<Event> SyncSender(Simulator env, Resource timesteps)
    {
        yield return env.SleepUntil(startTime);

        int TS = 0;
        while (TS < inputLayer.spikeSource.NrTimesteps())
        {
            yield return env.SleepUntil(startTime + interval * (TS + 1));

            yield return outBuffer.RequestWrite();
            outBuffer.Write(new SyncEvent()
            {
                TS = TS,
                CreatedAt = env.Now
            });
            outBuffer.ReleaseWrite();
            TimeAdvanced?.Invoke(this, TS);

            env.Increase(timesteps, 1);
            TS++;
        }
    }

    private IEnumerable<Event> Sender(Simulator env)
    {
        while (true)
        {
            yield return outBuffer.RequestRead();
            var flit = outBuffer.Read();
            yield return env.Process(SendEvent(env, flit));
            outBuffer.ReleaseRead();
        }
    }

    private IEnumerable<Event> Receiver(Simulator env)
    {
        while (true)
        {
            var rcv = env.Receive(Input);
            yield return rcv;
            var ev = rcv.Message;

            if (ev is MeshPacket)
            {
                var spike = (ev as MeshPacket).Message as SpikeEvent;
                SpikeReceived?.Invoke(this, env.Now, spike);
            }
            else
            {
                throw new Exception("Receiver received unknown message!");
            }
        }
    }

    private IEnumerable<Event> SendEvent(Simulator env, object message)
    {
        if (message is SpikeEvent)
        {
            var spikeEv = message as SpikeEvent;
            // Get the right desitination layer for the spike and also the coord to send it to
            var dest = mapping.CoordOf(spikeEv.Layer);
            var flit = new MeshPacket
            {
                Src = (MeshCoord)location,
                Dest = dest,
                Message = spikeEv
            };
            yield return env.Send(Output, flit);
        }
        else if (message is SyncEvent)
        {
            var timeEvent = message as SyncEvent;
            foreach (var core in mapping.Cores)
            {
                if (core is ControllerV1)
                    continue;

                var coord = (MeshCoord)core.GetLocation();
                var flit = new MeshPacket
                {
                    Src = (MeshCoord)(location),
                    Dest = coord,
                    Message = timeEvent
                };
                yield return env.Send(Output, flit);
            }
        }
        else
        {
            throw new Exception($"Unknown message: {message}");
        }
    }

    public bool AcceptsLayer(Layer layer) => false;

    public void AddLayer(Layer layer) { }

    string Core.Name() => this.Name;
}
