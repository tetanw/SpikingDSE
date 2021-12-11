using System;
using System.Collections.Generic;

namespace SpikingDSE;

public delegate void SpikeSent(ODINController controller, long time, ODINSpikeEvent spike);
public delegate void SpikeReceived(ODINController sink, long time, ODINSpikeEvent spike);
public delegate void TimeAdvanced(ODINController controller, int ts);


public sealed class ODINController : Actor, Core
{
    record StoredSpike(ODINSpikeEvent ODINSpike, bool fromRecurrence);


    public SpikeSent SpikeSent;
    public SpikeReceived SpikeReceived;
    public TimeAdvanced TimeAdvanced;

    public InPort spikesIn = new InPort();
    public OutPort spikesOut = new OutPort();

    private object location;
    private InputLayer inputLayer;
    private SNN snn;
    private long startTime;
    private long interval;
    private FIFO<object> outBuffer;
    private Queue<StoredSpike> storedSpikes = new();
    private Dictionary<Layer, MeshCoord> mappings = new();
    private Dictionary<IFLayer, RLIFLayer> convertedLayers = new();

    public ODINController(object location, SNN snn, long startTime, long interval, string name = null)
    {
        this.location = location;
        this.snn = snn;
        this.startTime = startTime;
        this.interval = interval;
        this.Name = name;
    }

    public void LayerToCoord(Layer layer, MeshCoord coreCoord)
    {
        mappings[layer] = coreCoord;
    }

    public void RLIF2LIF(RLIFLayer rlif, IFLayer lif)
    {
        convertedLayers[lif] = rlif;
    }

    public InPort GetIn() => spikesIn;

    public OutPort GetOut() => spikesOut;

    public object GetLocation() => location;

    public override IEnumerable<Event> Run(Environment env)
    {
        outBuffer = new(env, 1);

        env.Process(SpikeSender(env));

        env.Process(Sender(env));
        env.Process(Receiver(env));

        yield break;
    }

    private IEnumerable<Event> SpikeSender(Environment env)
    {
        yield return env.SleepUntil(startTime);
        int ts = 0;
        while (inputLayer.spikeSource.NextTimestep())
        {
            // Send spikes for input layer
            var inputSpikes = inputLayer.spikeSource.NeuronSpikes();
            foreach (var neuron in inputSpikes)
            {
                var spike = new ODINSpikeEvent(inputLayer, neuron);
                yield return outBuffer.RequestWrite();
                outBuffer.Write(new StoredSpike(spike, false));
                outBuffer.ReleaseWrite();
                SpikeSent?.Invoke(this, env.Now, spike);
            }

            // Send stored spikes for hidden layers
            while (storedSpikes.Count > 0)
            {
                var stored = storedSpikes.Dequeue();
                yield return outBuffer.RequestWrite();
                outBuffer.Write(stored);
                outBuffer.ReleaseWrite();
                SpikeSent?.Invoke(this, env.Now, stored.ODINSpike);
            }

            long syncTime = (env.Now / interval + 1) * interval;
            yield return env.SleepUntil(syncTime);
            yield return outBuffer.RequestWrite();
            outBuffer.Write(new ODINTimeEvent(ts));
            outBuffer.ReleaseWrite();
            TimeAdvanced?.Invoke(this, ts);
            ts++;
        }
    }

    private IEnumerable<Event> Sender(Environment env)
    {
        while (true)
        {
            yield return outBuffer.RequestRead();
            var flit = outBuffer.Read();
            foreach (var ev in SendODINEvent(env, flit))
            {
                yield return ev;
            }
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
                var spike = (ev as MeshFlit).Message as ODINSpikeEvent;
                SpikeReceived?.Invoke(this, env.Now, spike);

                // If from hidden layer then store output next layer then we do not have to do anything
                if (!snn.IsOutputLayer(spike.layer))
                {
                    // If from an RLIF converted layer then we need to make 1 extra spike
                    // i.e. for the recurrence
                    if (convertedLayers.ContainsKey((IFLayer)spike.layer))
                    {
                        RLIFLayer rlif = convertedLayers[(IFLayer)spike.layer];
                        int newNeuron = rlif.InputSize + spike.neuron;
                        storedSpikes.Enqueue(new StoredSpike(new ODINSpikeEvent(spike.layer, newNeuron), true));
                    }

                    storedSpikes.Enqueue(new StoredSpike(spike, false));
                }
            }
            else
            {
                throw new Exception("Receiver received unknown message!");
            }
        }
    }

    private IEnumerable<Event> SendODINEvent(Environment env, object message)
    {
        if (message is StoredSpike)
        {
            var (spikeEvent, fromRecurrence) = message as StoredSpike;
            // Get the right desitination layer for the spike and also the coord to send it to
            Layer destLayer;
            if (fromRecurrence)
            {
                destLayer = spikeEvent.layer;
            }
            else
            {
                destLayer = snn.GetDestLayer(spikeEvent.layer);
            }
            MeshCoord dest = mappings[destLayer];
            var flit = new MeshFlit
            {
                Src = (MeshCoord)location,
                Dest = dest,
                Message = spikeEvent
            };
            yield return env.Send(spikesOut, flit);
        }
        else if (message is ODINTimeEvent)
        {
            var timeEvent = message as ODINTimeEvent;
            foreach (var coord in mappings.Values)
            {
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
