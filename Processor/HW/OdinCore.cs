using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public struct ODINDelayModel
{
    public int InputTime;
    public int ComputeTime;
    public int OutputTime;
    public int TimeRefTime;
}

public sealed class OdinCore : Actor, ICore
{
    public delegate void SpikeReceived(OdinCore core, long time, SpikeEvent spike);
    public delegate void SpikeSent(OdinCore core, long time, SpikeEvent spike);
    public delegate void TimeReceived(OdinCore core, long time, int ts, OdinIFLayer layer);

    public SpikeReceived OnSpikeRecived;
    public SpikeSent OnSpikeSent;
    public TimeReceived OnTimeReceived;

    public InPort input = new();
    public OutPort output = new();

    private readonly object location;
    private CoreEvent received = null;
    private OdinIFLayer layer;
    private ODINDelayModel delayModel;
    private readonly int nrNeurons;
    public int totalOutputSpikes = 0;
    public int totalInputSpikes = 0;

    public OdinCore(object location, int nrNeurons, ODINDelayModel delayModel, string name = "")
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

        if (layer is OdinIFLayer)
        {
            var lifLayer = layer as OdinIFLayer;
            // check if enough fan-out and fan-in
            bool fanIn = lifLayer.InputSize < nrNeurons;
            bool fanOut = lifLayer.Size < nrNeurons;
            return fanIn && fanOut;
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

        this.layer = (OdinIFLayer)layer;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        while (true)
        {
            foreach (var ev in Receive(env))
            {
                yield return ev;
            }

            if (received is SpikeEvent)
            {
                foreach (var ev in Compute(env))
                {
                    yield return ev;
                }
            }
            else if (received is SyncEvent)
            {
                foreach (var ev in AdvanceTime(env))
                {
                    yield return ev;
                }
            }
            else
            {
                throw new Exception($"Unknown event: {received}");
            }
            received = null;
        }
    }

    private IEnumerable<Event> Compute(Simulator env)
    {
        var inputSpike = (SpikeEvent)received;
        OnSpikeRecived?.Invoke(this, env.Now, inputSpike);

        OdinIFLayer lif = (layer as OdinIFLayer);
        lif.Integrate(inputSpike.Neuron);
        long syncTime;
        long start = env.Now;
        int nrOutputSpikes = 0;
        foreach (var outputSpike in lif.Threshold())
        {
            nrOutputSpikes++;
            syncTime = start + (outputSpike + 1) * delayModel.ComputeTime + (nrOutputSpikes - 1) * delayModel.OutputTime;
            var outEvent = new SpikeEvent() { Layer = layer, Neuron = outputSpike, Feedback = false };
            OnSpikeSent?.Invoke(this, syncTime, outEvent);
            yield return env.SendAt(output, outEvent, syncTime);
        }
        syncTime = start + nrNeurons * delayModel.ComputeTime + nrOutputSpikes * delayModel.OutputTime;
        totalInputSpikes++;
        totalOutputSpikes += nrOutputSpikes;
        yield return env.SleepUntil(syncTime);
    }

    private IEnumerable<Event> Receive(Simulator env)
    {
        var rcv = env.Receive(input, transferTime: delayModel.InputTime);
        yield return rcv;
        received = (CoreEvent)rcv.Message;
    }

    private IEnumerable<Event> AdvanceTime(Simulator env)
    {
        OnTimeReceived?.Invoke(this, env.Now, (received as SyncEvent).TS, layer);
        totalOutputSpikes = 0;
        totalInputSpikes = 0;
        layer.Leak();
        received = null;
        yield return env.Delay(nrNeurons * delayModel.TimeRefTime);
    }

    string ICore.Name() => this.Name;

    public OutPort Output() => output;

    public InPort Input() => input;

    public double Energy(long now)
    {
        return 0.0;
    }

    public double Memory() => 0.0;

    public string Report(bool header) => string.Empty;
}



