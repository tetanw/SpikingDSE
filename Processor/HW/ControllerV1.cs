using System;
using System.Collections.Generic;

namespace SpikingDSE;


public sealed class ControllerV1 : Actor, Core
{
    public Action<Actor, long, SpikeEvent> SpikeSent;
    public Action<Actor, long, SpikeEvent> SpikeReceived;
    public Action<Actor, int> TimeAdvanced;

    public InPort spikesIn = new();
    public OutPort spikesOut = new();

    private object location;
    private InputLayer inputLayer;
    private long startTime;
    private long interval;
    private FIFO<object> outBuffer;
    private Queue<SpikeEvent> storedSpikes = new();
    private Mapping mapping;

    public ControllerV1(object location, int nrTimesteps, long startTime, long interval, string name = null)
    {
        this.location = location;
        this.startTime = startTime;
        this.interval = interval;
        this.Name = name;
    }

    public void LoadMapping(Mapping mapping)
    {
        this.mapping = mapping;
    }

    public InPort GetIn() => spikesIn;

    public OutPort GetOut() => spikesOut;

    public object GetLocation() => location;

    public override IEnumerable<Event> Run(Environment env)
    {
        outBuffer = new(env, 1);

        var timesteps = env.CreateResource(0);

        env.Process(SpikeSender(env, timesteps));
        env.Process(SyncSender(env, timesteps));

        env.Process(Sender(env));
        env.Process(Receiver(env));

        yield break;
    }

    private IEnumerable<Event> SpikeSender(Environment env, Resource timesteps)
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
                    var spike = new SpikeEvent(destLayer, neuron, false, TS);
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

    private IEnumerable<Event> SyncSender(Environment env, Resource timesteps)
    {
        yield return env.SleepUntil(startTime);

        int TS = 0;
        while (TS < inputLayer.spikeSource.NrTimesteps())
        {
            yield return env.SleepUntil(startTime + interval * (TS + 1));

            yield return outBuffer.RequestWrite();
            outBuffer.Write(new SyncEvent(TS));
            outBuffer.ReleaseWrite();
            TimeAdvanced?.Invoke(this, TS);

            env.IncreaseResource(timesteps, 1);
            TS++;
        }
    }

    private IEnumerable<Event> Sender(Environment env)
    {
        while (true)
        {
            yield return outBuffer.RequestRead();
            var flit = outBuffer.Read();
            yield return env.Process(SendEvent(env, flit));
            outBuffer.ReleaseRead();
        }
    }

    private IEnumerable<Event> Receiver(Environment env)
    {
        while (true)
        {
            var rcv = env.Receive(spikesIn);
            yield return rcv;
            var ev = rcv.Message;

            if (ev is MeshFlit)
            {
                var spike = (ev as MeshFlit).Message as SpikeEvent;
                SpikeReceived?.Invoke(this, env.Now, spike);
            }
            else
            {
                throw new Exception("Receiver received unknown message!");
            }
        }
    }

    private IEnumerable<Event> SendEvent(Environment env, object message)
    {
        if (message is SpikeEvent)
        {
            var spikeEv = message as SpikeEvent;
            // Get the right desitination layer for the spike and also the coord to send it to
            var dest = mapping.CoordOf(spikeEv.layer);
            var flit = new MeshFlit
            {
                Src = (MeshCoord)location,
                Dest = dest,
                Message = spikeEv
            };
            yield return env.Send(spikesOut, flit);
        }
        else if (message is SyncEvent)
        {
            var timeEvent = message as SyncEvent;
            foreach (var core in mapping.Cores)
            {
                var coord = (MeshCoord)core.GetLocation();
                var flit = new MeshFlit
                {
                    Src = (MeshCoord)(location),
                    Dest = coord,
                    Message = timeEvent
                };
                yield return env.Send(spikesOut, flit);
            }
        }
        else
        {
            throw new Exception($"Unknown message: {message}");
        }
    }

    public bool AcceptsLayer(Layer layer)
    {
        return layer is InputLayer;
    }

    public void AddLayer(Layer layer)
    {
        if (layer is InputLayer)
        {
            this.inputLayer = (InputLayer)layer;
        }
        else
        {
            throw new Exception($"Does not accept layer {layer}");
        }
    }
}
