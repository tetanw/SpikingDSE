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

public sealed class CoreV1 : Actor, Core
{
    public delegate void SpikeReceived(CoreV1 core, long time, Layer layer, int neuron, bool feedback);
    public delegate void SpikeSent(CoreV1 core, long time, SpikeEvent spike);
    public delegate void SyncStarted(CoreV1 core, long time, int ts, HiddenLayer layer);
    public delegate void SyncEnded(CoreV1 core, long time, int ts, HiddenLayer layer);

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public SyncStarted OnSyncStarted;
    public SyncEnded OnSyncEnded;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private MeshCoord location;
    private Mapping mapping;
    private List<HiddenLayer> layers = new();
    private V1DelayModel delayModel;
    private int maxNrNeurons;
    private int totalOutputSpikes = 0;
    private int totalInputSpikes = 0;
    private int feedbackBufferSize;
    private Queue<SpikeEvent> feedback = new Queue<SpikeEvent>();

    public CoreV1(MeshCoord location, int maxNrNeurons, V1DelayModel delayModel, int feedbackBufferSize = int.MaxValue, string name = "")
    {
        this.location = location;
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

    public object GetLocation() => location;

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
            if (feedback.Count > 0)
            {
                yield return env.Process(Feedback(env, feedback.Dequeue()));
            }
            else
            {
                CoreEvent received = null;
                yield return env.Process(Receive(env, (recv) => received = recv));
                if (received is SpikeEvent)
                {
                    var spikeEvent = (SpikeEvent)received;
                    yield return env.Process(Compute(env, spikeEvent));
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

    private IEnumerable<Event> Feedback(Environment env, SpikeEvent spike)
    {
        var (layer, neuron) = spike;
        OnSpikeReceived?.Invoke(this, env.Now, layer, neuron, true);
        (layer as ALIFLayer).Feedback(neuron); // source layer = dest layer so no lookup needed
        yield return env.Delay(delayModel.ComputeTime * layer.Size);
    }

    private IEnumerable<Event> Compute(Environment env, SpikeEvent spike)
    {
        var (layer, neuron) = spike;
        OnSpikeReceived?.Invoke(this, env.Now, layer, neuron, false);
        (mapping.GetDestLayer(layer) as HiddenLayer).Forward(neuron);
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
            long syncTime = -1;
            long start = env.Now;
            int nrOutputSpikes = 0;
            foreach (var spikingNeuron in layer.Sync())
            {
                nrOutputSpikes++;
                syncTime = start + (spikingNeuron + 1) * delayModel.ComputeTime + (nrOutputSpikes - 1) * delayModel.OutputTime;
                var outEvent = new SpikeEvent(layer, spikingNeuron);
                OnSpikeSent?.Invoke(this, syncTime, outEvent);
                if (layer is ALIFLayer && feedback.Count <= feedbackBufferSize)
                {
                    feedback.Enqueue(outEvent);
                }
                var flit = new MeshFlit
                {
                    Src = location,
                    Dest = mapping.CoordOf(mapping.GetDestLayer(layer)),
                    Message = outEvent
                };
                yield return env.SendAt(output, flit, syncTime);
            }
            syncTime = start + maxNrNeurons * delayModel.ComputeTime + nrOutputSpikes * delayModel.OutputTime;
            totalInputSpikes++;
            totalOutputSpikes += nrOutputSpikes;
            yield return env.SleepUntil(syncTime);

            OnSyncEnded?.Invoke(this, env.Now, TS, layer);
        }
    }
}



