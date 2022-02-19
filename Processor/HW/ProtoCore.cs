using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public struct ProtoDelayModel
{
    public int InputTime;
    public int ComputeTime;
    public int OutputTime;
    public int TimeRefTime;
}

public sealed class ProtoCore : Actor, Core
{
    public delegate void SpikeReceived(ProtoCore core, long time, Layer layer, int neuron, bool feedback);
    public delegate void SpikeSent(ProtoCore core, long time, SpikeEvent spike);
    public delegate void SyncStarted(ProtoCore core, long time, int ts, HiddenLayer layer);
    public delegate void SyncEnded(ProtoCore core, long time, int ts, HiddenLayer layer);

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public SyncStarted OnSyncStarted;
    public SyncEnded OnSyncEnded;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private MeshCoord location, destination;
    private HiddenLayer Layer;
    private ProtoDelayModel delayModel;
    private int nrNeurons;
    private int totalOutputSpikes = 0;
    private int totalInputSpikes = 0;
    private int feedbackBufferSize;
    private Queue<int> feedback = new Queue<int>();

    public ProtoCore(MeshCoord location, int nrNeurons, ProtoDelayModel delayModel, int feedbackBufferSize = int.MaxValue, string name = "")
    {
        this.location = location;
        this.Name = name;
        this.nrNeurons = nrNeurons;
        this.feedbackBufferSize = feedbackBufferSize;
        this.delayModel = delayModel;
    }

    public bool AcceptsLayer(Layer layer)
    {
        // The ODIN core can only accept 1 layer for now
        if (this.Layer != null)
        {
            return false;
        }

        if (layer is HiddenLayer)
        {
            var lifLayer = (HiddenLayer)layer;
            return true;
        }
        else
        {
            return false;
        }
    }

    public object GetLocation() => location;

    public void AddLayer(Layer layer)
    {
        if (this.Layer != null)
            throw new Exception("Only accepts 1 layer");

        this.Layer = (HiddenLayer)layer;
    }

    public void setDestination(MeshCoord coord)
    {
        this.destination = coord;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        while (true)
        {
            if (feedback.Count > 0)
            {
                yield return env.Process(Compute(env, feedback.Dequeue(), true));
            }
            else
            {
                CoreEvent received = null;
                yield return env.Process(Receive(env, (recv) => received = recv));
                if (received is SpikeEvent)
                {
                    var spikeEvent = (SpikeEvent)received;
                    yield return env.Process(Compute(env, spikeEvent.Neuron, false));
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

    private IEnumerable<Event> Compute(Simulator env, int neuron, bool feedback)
    {
        OnSpikeReceived?.Invoke(this, env.Now, Layer, neuron, feedback);
        if (feedback)
        {
            (Layer as RLIFLayer2)?.Feedback(neuron);
            (Layer as ALIFLayer)?.Feedback(neuron);
        }
        else
        {
            Layer.Forward(neuron);
        }

        // HW v1
        yield return env.Delay(delayModel.ComputeTime * nrNeurons);
        // HW v2
        // yield return env.Delay(delayModel.ComputeTime * this.Layer.Size);
    }

    private IEnumerable<Event> Receive(Simulator env, Action<CoreEvent> onReceive)
    {
        var rcv = env.Receive(input, transferTime: delayModel.InputTime);
        yield return rcv;
        var flit = (MeshPacket)rcv.Message;
        onReceive((CoreEvent)flit.Message);
    }

    private IEnumerable<Event> Sync(Simulator env, int TS)
    {
        totalOutputSpikes = 0;
        totalInputSpikes = 0;

        // Readout of timestep TS - 1
        OnSyncStarted?.Invoke(this, env.Now, TS, Layer);

        // Threshold of timestep TS - 1
        long syncTime = -1;
        long start = env.Now;
        int nrOutputSpikes = 0;
        foreach (var spikingNeuron in Layer.Sync())
        {
            nrOutputSpikes++;
            syncTime = start + (spikingNeuron + 1) * delayModel.ComputeTime + (nrOutputSpikes - 1) * delayModel.OutputTime;
            var outEvent = new SpikeEvent() { Layer = Layer, Neuron = spikingNeuron, Feedback = false };
            OnSpikeSent?.Invoke(this, syncTime, outEvent);
            if ((Layer is RLIFLayer2 || Layer is ALIFLayer) && feedback.Count <= feedbackBufferSize) feedback.Enqueue(spikingNeuron);
            yield return env.SendAt(output, new MeshPacket { Src = location, Dest = destination, Message = outEvent }, syncTime);
        }
        syncTime = start + nrNeurons * delayModel.ComputeTime + nrOutputSpikes * delayModel.OutputTime;
        totalInputSpikes++;
        totalOutputSpikes += nrOutputSpikes;
        yield return env.SleepUntil(syncTime);

        OnSyncEnded?.Invoke(this, env.Now, TS, Layer);
    }

    string Core.Name() => this.Name;
}



