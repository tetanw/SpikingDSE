using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public struct V1DelayModel
{
    public int InputTime;
    public int ComputeTime;
    public int OutputTime;
    public int TimeRefTime;
}

public record StoredSpike(Layer layer, int neuron, bool feedback);

public sealed class CoreV1 : Actor, Core
{
    public delegate void SpikeReceived(CoreV1 core, long time, Layer layer, int neuron, bool feedback);
    public delegate void SpikeSent(CoreV1 core, long time, Layer from, int neuron);
    public delegate void SyncStarted(CoreV1 core, long time, int ts, HiddenLayer layer);
    public delegate void SyncEnded(CoreV1 core, long time, int ts, HiddenLayer layer);

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public SyncStarted OnSyncStarted;
    public SyncEnded OnSyncEnded;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private MeshCoord thisLoc;
    private Mapping mapping;
    private List<HiddenLayer> layers = new();
    private V1DelayModel delayModel;
    private int maxNrNeurons;
    private int totalOutputSpikes = 0;
    private int totalInputSpikes = 0;
    private int TS = 0;
    private int feedbackBufferSize;
    private Queue<SpikeEvent> feedbackBuffer = new();

    public CoreV1(MeshCoord location, int maxNrNeurons, V1DelayModel delayModel, int feedbackBufferSize = int.MaxValue, string name = "")
    {
        this.thisLoc = location;
        this.Name = name;
        this.maxNrNeurons = maxNrNeurons;
        this.feedbackBufferSize = feedbackBufferSize;
        this.delayModel = delayModel;
    }

    public bool AcceptsLayer(Layer layer)
    {
        switch (layer)
        {
            case HiddenLayer hl:
                int nrNeurons = layers.Sum(l => l.Size);
                return nrNeurons + hl.Size <= maxNrNeurons;
            default:
                return false;
        }
    }

    public InPort GetIn() => input;

    public OutPort GetOut() => output;

    public object GetLocation() => thisLoc;

    public void AddLayer(Layer layer)
    {
        var hl = (HiddenLayer)layer;
        layers.Add(hl);
    }

    public void LoadMapping(Mapping mapping)
    {
        this.mapping = mapping;
    }

    public override IEnumerable<Event> Run(Environment env)
    {
        while (true)
        {
            if (feedbackBuffer.Count > 0)
            {
                var (layer, neuron, feedback) = feedbackBuffer.Dequeue();

                if (feedback)
                {
                    yield return env.Process(Feedback(env, layer, neuron));
                }
                else
                {
                    yield return env.Process(Compute(env, layer, neuron));
                }
            }
            else
            {
                CoreEvent received = null;
                yield return env.Process(Receive(env, (recv) => received = recv));
                if (received is SpikeEvent)
                {
                    var (layer, neuron, feedback) = (SpikeEvent)received;

                    if (feedback)
                    {
                        yield return env.Process(Feedback(env, layer, neuron));
                    }
                    else
                    {
                        yield return env.Process(Compute(env, layer, neuron));
                    }
                }
                else if (received is SyncEvent)
                {
                    var timeEvent = (SyncEvent)received;
                    yield return env.Process(Sync(env, timeEvent.TS));
                }
                else
                {
                    throw new Exception($"Unknown event: {received}");
                }
            }
        }
    }

    private IEnumerable<Event> Feedback(Environment env, Layer layer, int neuron)
    {
        OnSpikeReceived?.Invoke(this, env.Now, layer, neuron, true);
        (layer as ALIFLayer).Feedback(neuron);
        yield return env.Delay(delayModel.ComputeTime * layer.Size);
    }

    private IEnumerable<Event> Compute(Environment env, Layer layer, int neuron)
    {
        OnSpikeReceived?.Invoke(this, env.Now, layer, neuron, false);
        (layer as HiddenLayer).Forward(neuron);
        yield return env.Delay(delayModel.ComputeTime * layer.Size);
    }

    private IEnumerable<Event> Receive(Environment env, Action<CoreEvent> onReceive)
    {
        var rcv = env.Receive(input, waitBefore: delayModel.InputTime);
        yield return rcv;
        var flit = (MeshFlit)rcv.Message;
        onReceive((CoreEvent)flit.Message);
    }

    private IEnumerable<Event> Sync(Environment env, int TS)
    {
        totalOutputSpikes = 0;
        totalInputSpikes = 0;
        foreach (var layer in layers)
        {
            // Readout of timestep TS - 1
            OnSyncStarted?.Invoke(this, env.Now, TS, layer);

            // Threshold of timestep TS - 1
            int nrOutputSpikes = 0;
            foreach (var spikingNeuron in layer.Sync())
            {
                nrOutputSpikes++;
                int offset = (layer as ALIFLayer).Offset;

                // Feedback spikes
                foreach (var sibling in mapping.GetSiblings(layer))
                {
                    var spikeEv = new SpikeEvent(sibling, spikingNeuron + offset, true);
                    var siblingCoord = mapping.CoordOf(sibling);
                    if (siblingCoord == thisLoc)
                    {
                        if (layer is ALIFLayer && feedbackBuffer.Count < feedbackBufferSize)
                        {
                            feedbackBuffer.Enqueue(spikeEv);
                        }
                    }
                    else
                    {
                        // Send recurrent spikes to other core
                        var flit = new MeshFlit
                        {
                            Src = thisLoc,
                            Dest = siblingCoord,
                            Message = spikeEv
                        };
                    }

                }

                // Forward spikes
                foreach (var destLayer in mapping.GetDestLayers(layer))
                {
                    var spikeEv = new SpikeEvent(destLayer, spikingNeuron + offset, false);
                    var destCoord = mapping.CoordOf(destLayer);
                    if (destCoord == thisLoc)
                    {
                        if (feedbackBuffer.Count < feedbackBufferSize)
                            feedbackBuffer.Enqueue(spikeEv);
                    }
                    else
                    {
                        var flit = new MeshFlit
                        {
                            Src = thisLoc,
                            Dest = destCoord,
                            Message = spikeEv
                        };
                        yield return env.Send(output, flit);
                    }
                    OnSpikeSent?.Invoke(this, env.Now, layer, spikingNeuron);
                }
            }
            totalInputSpikes++;
            totalOutputSpikes += nrOutputSpikes;

            OnSyncEnded?.Invoke(this, env.Now, TS, layer);
        }

        this.TS = TS + 1;
    }

    public override string ToString()
    {
        return $"{this.Name}";
    }
}



