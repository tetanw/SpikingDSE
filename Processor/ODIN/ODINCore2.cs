using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public sealed class ODINCore2 : Actor, Core
{
    public delegate void SpikeReceived(ODINCore2 core, long time, Layer layer, int neuron, bool feedback);
    public delegate void SpikeSent(ODINCore2 core, long time, ODINSpikeEvent spike);
    public delegate void TimeReceived(ODINCore2 core, long time, int ts, RLIFLayer layer);

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public TimeReceived OnTimeReceived;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private Object location;
    private RLIFLayer layer;
    private ODINDelayModel delayModel;
    private int nrNeurons;
    private int totalOutputSpikes = 0;
    private int totalInputSpikes = 0;
    private Queue<int> feedback = new Queue<int>();

    public ODINCore2(object location, int nrNeurons, ODINDelayModel delayModel, string name = "")
    {
        this.location = location;
        this.Name = name;
        this.nrNeurons = nrNeurons;
        this.delayModel = delayModel;
    }

    public bool AcceptsLayer(Layer layer)
    {
        // The ODIN core can only accept 1 layer for now
        if (this.layer != null)
        {
            return false;
        }

        if (layer is RLIFLayer)
        {
            var lifLayer = layer as RLIFLayer;
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
        if (this.layer != null)
            throw new Exception("Only accepts 1 layer");

        this.layer = (RLIFLayer)layer;
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
            layer.ApplyThreshold(neuron);
            layer.IntegrateFeedback(neuron);
        }
        else
        {
            OnSpikeReceived?.Invoke(this, env.Now, layer, neuron, feedback);
            layer.IntegrateForward(neuron);
        }

        yield break;
    }

    private IEnumerable<Event> Receive(Environment env, Action<ODINEvent> onReceive)
    {
        var rcv = env.Receive(input, waitBefore: delayModel.InputTime);
        yield return rcv;
        onReceive((ODINEvent)rcv.Message);
    }

    private IEnumerable<Event> AdvanceTime(Environment env, int TS)
    {
        totalOutputSpikes = 0;
        totalInputSpikes = 0;

        // Readout of timestep TS - 1
        OnTimeReceived?.Invoke(this, env.Now, TS, layer);

        // Threshold of timestep TS - 1
        long syncTime = -1;
        long start = env.Now;
        int nrOutputSpikes = 0;
        foreach (var spikingNeuron in layer.Threshold())
        {
            nrOutputSpikes++;
            syncTime = start + (spikingNeuron + 1) * delayModel.ComputeTime + (nrOutputSpikes - 1) * delayModel.OutputTime;
            var outEvent = new ODINSpikeEvent(layer, spikingNeuron);
            OnSpikeSent?.Invoke(this, syncTime, outEvent);
            feedback.Enqueue(spikingNeuron);
            yield return env.SendAt(output, outEvent, syncTime);
        }
        syncTime = start + nrNeurons * delayModel.ComputeTime + nrOutputSpikes * delayModel.OutputTime;
        totalInputSpikes++;
        totalOutputSpikes += nrOutputSpikes;
        yield return env.SleepUntil(syncTime);

        // Leakage of timestep TS 
        layer.Leak();
        yield return env.Delay(nrNeurons * delayModel.TimeRefTime);
    }
}



