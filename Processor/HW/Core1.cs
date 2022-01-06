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

public abstract record ODINEvent();
public sealed record ODINTimeEvent(int TS) : ODINEvent;
public sealed record ODINSpikeEvent(Layer layer, int neuron) : ODINEvent;

public sealed class Core1 : Actor, Core
{
    public delegate void SpikeReceived(Core1 core, long time, ODINSpikeEvent spike);
    public delegate void SpikeSent(Core1 core, long time, ODINSpikeEvent spike);
    public delegate void TimeReceived(Core1 core, long time, int ts, IFLayer layer);

    public SpikeReceived OnSpikeRecived;
    public SpikeSent OnSpikeSent;
    public TimeReceived OnTimeReceived;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private Object location;
    private ODINEvent received = null;
    private IFLayer layer;
    private ODINDelayModel delayModel;
    private int nrNeurons;
    private bool enableRefractory;
    private int totalOutputSpikes = 0;
    private int totalInputSpikes = 0;

    public Core1(object location, int nrNeurons, ODINDelayModel delayModel, string name = "", bool enableRefractory = false)
    {
        this.location = location;
        this.Name = name;
        this.nrNeurons = nrNeurons;
        this.delayModel = delayModel;
        this.enableRefractory = enableRefractory;
    }

    public bool AcceptsLayer(Layer layer)
    {
        // The ODIN core can only accept 1 layer for now
        if (this.layer != null)
        {
            return false;
        }

        if (layer is IFLayer)
        {
            var lifLayer = layer as IFLayer;
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

        this.layer = (IFLayer)layer;
    }

    public override IEnumerable<Event> Run(Environment env)
    {
        while (true)
        {
            foreach (var ev in Receive(env))
            {
                yield return ev;
            }

            if (received is ODINSpikeEvent)
            {
                foreach (var ev in Compute(env))
                {
                    yield return ev;
                }
            }
            else if (received is ODINTimeEvent)
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

    private IEnumerable<Event> Compute(Environment env)
    {
        var inputSpike = (ODINSpikeEvent)received;
        OnSpikeRecived?.Invoke(this, env.Now, inputSpike);

        IFLayer lif = (layer as IFLayer);
        lif.Integrate(inputSpike.neuron);
        long syncTime = -1;
        long start = env.Now;
        int nrOutputSpikes = 0;
        foreach (var outputSpike in lif.Threshold())
        {
            nrOutputSpikes++;
            syncTime = start + (outputSpike + 1) * delayModel.ComputeTime + (nrOutputSpikes - 1) * delayModel.OutputTime;
            var outEvent = new ODINSpikeEvent(layer, outputSpike);
            OnSpikeSent?.Invoke(this, syncTime, outEvent);
            yield return env.SendAt(output, outEvent, syncTime);
        }
        syncTime = start + nrNeurons * delayModel.ComputeTime + nrOutputSpikes * delayModel.OutputTime;
        totalInputSpikes++;
        totalOutputSpikes += nrOutputSpikes;
        yield return env.SleepUntil(syncTime);
    }

    private IEnumerable<Event> Receive(Environment env)
    {
        var rcv = env.Receive(input, waitBefore: delayModel.InputTime);
        yield return rcv;
        received = (ODINEvent)rcv.Message;
    }

    private IEnumerable<Event> AdvanceTime(Environment env)
    {
        OnTimeReceived?.Invoke(this, env.Now, (received as ODINTimeEvent).TS, layer);
        totalOutputSpikes = 0;
        totalInputSpikes = 0;
        layer.Leak();
        received = null;
        yield return env.Delay(nrNeurons * delayModel.TimeRefTime);
    }
}



