using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public delegate void ReceivedSpike(ODINCore core, long time, ODINSpikeEvent spike);
public delegate void ProducedSpike(ODINCore core, long time, ODINSpikeEvent spike);
public delegate void ReceivedTimeref(ODINCore core, long time);

public struct ODINDelayModel
{
    public int InputTime;
    public int ComputeTime;
    public int OutputTime;
    public int TimeRefTime;
}

public abstract record ODINEvent();
public sealed record ODINTimeEvent() : ODINEvent;
public sealed record ODINSpikeEvent(Layer layer, int neuron) : ODINEvent;

public sealed class ODINCore : Actor, Core
{
    public ProducedSpike ProducedSpike;
    public ReceivedSpike ReceivedSpike;
    public ReceivedTimeref ReceivedTimeref;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private Object location;
    private ODINEvent received = null;
    private LIFLayer layer;
    private ODINDelayModel delayModel;
    private int nrNeurons;
    private bool enableRefractory;

    public ODINCore(object location, int nrNeurons, ODINDelayModel delayModel, string name = "", bool enableRefractory = false)
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

        if (layer is LIFLayer)
        {
            var lifLayer = layer as LIFLayer;
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

        this.layer = (LIFLayer)layer;
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
        // FIXME: Debug
        var inputSpike = (ODINSpikeEvent)received;
        ReceivedSpike?.Invoke(this, env.Now, inputSpike);

        LIFLayer lif = (layer as LIFLayer);
        lif.Integrate(inputSpike.neuron);
        int prevSpike = 0;
        long syncTime = -1;
        foreach (var outputSpike in lif.Threshold())
        {
            syncTime = env.Now + (outputSpike - prevSpike) * delayModel.ComputeTime + delayModel.OutputTime;
            var outEvent = new ODINSpikeEvent(layer, outputSpike);
            ProducedSpike?.Invoke(this, syncTime, outEvent);
            yield return env.SendAt(output, outEvent, syncTime);
        }
        syncTime = env.Now + (256 - prevSpike) * delayModel.ComputeTime;
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
        ReceivedTimeref?.Invoke(this, env.Now);
        layer.Leak();
        yield return env.Delay(nrNeurons * delayModel.TimeRefTime);
    }
}



