using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public sealed class ODINCore2 : Actor, Core
{
    public delegate void SpikeReceived(ODINCore2 core, long time, Layer layer, int neuron, bool feedback);
    public delegate void SpikeSent(ODINCore2 core, long time, ODINSpikeEvent spike);
    public delegate void TimeReceived(ODINCore2 core, long time, int ts, HiddenLayer layer);

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public TimeReceived OnTimeReceived;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private MeshCoord location, destination;
    private HiddenLayer Layer;
    private ODINDelayModel delayModel;
    private int nrNeurons;
    private int totalOutputSpikes = 0;
    private int totalInputSpikes = 0;
    private Queue<int> feedback = new Queue<int>();
    private int feedbackBufferSize;

    public ODINCore2(MeshCoord location, int nrNeurons, ODINDelayModel delayModel, int feedbackBufferSize = int.MaxValue, string name = "")
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
            // TODO: Add extra checks nr neurons etc...
            return true;
        }
        else
        {
            return false;
        }
    }

    public InPort GetIn() => input;

    public OutPort GetOut() => output;

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

    public override IEnumerable<Event> Run(Environment env)
    {
        while (true)
        {
            if (feedback.Count > 0)
            {
                int spikingNeuron = feedback.Dequeue();
                yield return env.Process(Compute(env, spikingNeuron, true));
            }
            else
            {
                ODINEvent received = null;
                yield return env.Process(Receive(env, (recv) => received = recv));
                if (received is ODINSpikeEvent)
                {
                    var spikeEvent = (ODINSpikeEvent)received;
                    yield return env.Process(Compute(env, spikeEvent.neuron, false));
                }
                else if (received is ODINTimeEvent)
                {
                    var timeEvent = (ODINTimeEvent)received;
                    yield return env.Process(AdvanceTime(env, timeEvent.TS));
                }
                else
                {
                    throw new Exception($"Unknown event: {received}");
                }
            }
        }
    }

    private IEnumerable<Event> Compute(Environment env, int neuron, bool feedback)
    {
        if (feedback)
        {
            Layer.ApplyThreshold(neuron);
            ((RLIFLayer)Layer).IntegrateFeedback(neuron);
        }
        else
        {
            OnSpikeReceived?.Invoke(this, env.Now, Layer, neuron, feedback);
            Layer.Integrate(neuron);
        }

        // HW v1
        yield return env.Delay(delayModel.ComputeTime * nrNeurons);
        // HW v2
        // yield return env.Delay(delayModel.ComputeTime * this.Layer.Size);
    }

    private IEnumerable<Event> Receive(Environment env, Action<ODINEvent> onReceive)
    {
        var rcv = env.Receive(input, waitBefore: delayModel.InputTime);
        yield return rcv;
        var flit = (MeshFlit)rcv.Message;
        onReceive((ODINEvent)flit.Message);
    }

    private IEnumerable<Event> AdvanceTime(Environment env, int TS)
    {
        totalOutputSpikes = 0;
        totalInputSpikes = 0;

        // Readout of timestep TS - 1
        OnTimeReceived?.Invoke(this, env.Now, TS, Layer);

        // Threshold of timestep TS - 1
        long syncTime = -1;
        long start = env.Now;
        int nrOutputSpikes = 0;
        foreach (var spikingNeuron in Layer.Threshold())
        {
            nrOutputSpikes++;
            syncTime = start + (spikingNeuron + 1) * delayModel.ComputeTime + (nrOutputSpikes - 1) * delayModel.OutputTime;
            var outEvent = new ODINSpikeEvent(Layer, spikingNeuron);
            OnSpikeSent?.Invoke(this, syncTime, outEvent);
            if (Layer is RLIFLayer && feedback.Count < feedbackBufferSize)
                feedback.Enqueue(spikingNeuron);
            yield return env.SendAt(output, new MeshFlit { Src = location, Dest = destination, Message = outEvent }, syncTime);
        }
        syncTime = start + nrNeurons * delayModel.ComputeTime + nrOutputSpikes * delayModel.OutputTime;
        totalInputSpikes++;
        totalOutputSpikes += nrOutputSpikes;
        yield return env.SleepUntil(syncTime);

        // Leakage of timestep TS
        Layer.Leak();
        yield return env.Delay(nrNeurons * delayModel.TimeRefTime);
    }
}



