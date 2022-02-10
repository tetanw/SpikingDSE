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
    // TODO: Consider removing core as it is kind of redundant
    public delegate void SpikeReceived(long time, Layer layer, int neuron, bool feedback, SpikeEvent spike);
    public delegate void SpikeSent(long time, Layer from, int neuron, SpikeEvent spike);
    public delegate void SyncStarted(long time, int ts, HiddenLayer layer);
    public delegate void SyncEnded(long time, int ts, HiddenLayer layer);
    public delegate void SpikeComputed(long time, SpikeEvent spike);

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public SyncStarted OnSyncStarted;
    public SyncEnded OnSyncEnded;
    public SpikeComputed OnSpikeComputed;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private MeshCoord thisLoc;
    private MappingTable mapping;
    private V1DelayModel delayModel;
    private int totalOutputSpikes = 0;
    private int totalInputSpikes = 0;
    private int bufferSize;
    private FIFO<CoreEvent> coreBuffer;
    private FIFO<CoreEvent> inputBuffer;

    public CoreV1(MeshCoord location, V1DelayModel delayModel, int feedbackBufferSize = int.MaxValue, string name = "")
    {
        this.thisLoc = location;
        this.Name = name;
        this.bufferSize = feedbackBufferSize;
        this.delayModel = delayModel;
    }

    public bool AcceptsLayer(Layer layer) => false;

    public object GetLocation() => thisLoc;

    public void AddLayer(Layer layer) { }

    public void LoadMapping(MappingTable mapping)
    {
        this.mapping = mapping;
    }

    private IEnumerable<Event> Receiver(Environment env)
    {
        int TS = 0;

        while (true)
        {
            var rcv = env.Receive(input);
            yield return rcv;
            var flit = (MeshFlit)rcv.Message;
            var @event = flit.Message as CoreEvent;

            switch (@event)
            {
                case SyncEvent sync:
                    if (!coreBuffer.IsFull)
                    {
                        yield return coreBuffer.RequestWrite();
                        coreBuffer.Write(sync);
                        coreBuffer.ReleaseWrite();
                    }

                    // write all spikes that were waiting for sync event to happen
                    while (!inputBuffer.IsEmpty)
                    {
                        var spike = (SpikeEvent)inputBuffer.Pop();
                        if (!coreBuffer.IsFull)
                            coreBuffer.Push(spike);
                    }

                    TS = sync.TS + 1;
                    break;
                case SpikeEvent spike:
                    spike.ReceivedAt = env.Now;
                    OnSpikeReceived?.Invoke(env.Now, spike.Layer, spike.Neuron, spike.Feedback, spike);

                    if (spike.TS > TS)
                    {
                        if (!inputBuffer.IsFull)
                            inputBuffer.Push(spike);
                    }
                    else if (spike.TS == TS)
                    {
                        if (!coreBuffer.IsFull)
                            coreBuffer.Push(spike);
                    }
                    else
                    {
                        // Else drop spike
                    }
                    break;
                default:
                    throw new Exception("Unknown event when handling input: " + @event);
            }


        }
    }

    public override IEnumerable<Event> Run(Environment env)
    {
        inputBuffer = new(env, bufferSize);
        coreBuffer = new(env, bufferSize);
        env.Process(Receiver(env));

        while (true)
        {
            yield return coreBuffer.RequestRead();
            var core = coreBuffer.Read();
            coreBuffer.ReleaseRead();

            switch (core)
            {
                case SyncEvent sync:
                    yield return env.Process(Sync(env, sync));
                    break;
                case SpikeEvent spike:
                    yield return env.Process(Compute(env, spike));
                    break;
                default:
                    throw new Exception("Unknown event!");
            }
        }
    }

    private IEnumerable<Event> Compute(Environment env, SpikeEvent spike)
    {
        OnSpikeComputed?.Invoke(env.Now, spike);
        if (spike.Feedback)
        {
            (spike.Layer as ALIFLayer).Feedback(spike.Neuron);
        }
        else
        {
            (spike.Layer as HiddenLayer).Forward(spike.Neuron);
        }
        yield return env.Delay(delayModel.ComputeTime * spike.Layer.Size);
    }

    private IEnumerable<Event> Sync(Environment env, SyncEvent sync)
    {
        totalOutputSpikes = 0;
        totalInputSpikes = 0;
        foreach (var l in mapping[this])
        {
            var layer = (HiddenLayer)l;

            // Readout of timestep TS - 1
            OnSyncStarted?.Invoke(env.Now, sync.TS, layer);

            // Threshold of timestep TS - 1
            int nrOutputSpikes = 0;
            int lastSpikingNeuron = 0;
            foreach (var spikingNeuron in layer.Sync())
            {
                var neuronsComputed = lastSpikingNeuron - spikingNeuron;
                yield return env.Delay(delayModel.ComputeTime * neuronsComputed);
                lastSpikingNeuron = spikingNeuron;

                nrOutputSpikes++;
                int offset = (layer as ALIFLayer).Offset;

                // Feedback spikes
                foreach (var sibling in mapping.GetSiblings(layer))
                {
                    var spikeEv = new SpikeEvent()
                    {
                        Layer = sibling,
                        Neuron = spikingNeuron + offset,
                        Feedback = true,
                        TS = sync.TS + 1,
                        CreatedAt = env.Now
                    };
                    var siblingCoord = mapping.CoordOf(sibling);
                    if (siblingCoord == thisLoc)
                    {
                        if (layer is ALIFLayer && !coreBuffer.IsFull)
                        {
                            spikeEv.ReceivedAt = env.Now;
                            coreBuffer.Push(spikeEv);
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
                        yield return env.Delay(delayModel.OutputTime);
                        yield return env.Send(output, flit);
                    }

                }

                // Forward spikes
                foreach (var destLayer in mapping.GetDestLayers(layer))
                {
                    var spikeOut = new SpikeEvent()
                    {
                        Layer = destLayer,
                        Neuron = spikingNeuron + offset,
                        Feedback = false,
                        TS = sync.TS + 1,
                        CreatedAt = env.Now
                    };
                    var destCoord = mapping.CoordOf(destLayer);
                    if (destCoord == thisLoc)
                    {
                        spikeOut.ReceivedAt = env.Now;
                        if (!coreBuffer.IsFull)
                            coreBuffer.Push(spikeOut);
                    }
                    else
                    {
                        var flit = new MeshFlit
                        {
                            Src = thisLoc,
                            Dest = destCoord,
                            Message = spikeOut
                        };
                        yield return env.Delay(delayModel.OutputTime);
                        yield return env.Send(output, flit);
                    }
                    OnSpikeSent?.Invoke(env.Now, layer, spikingNeuron, spikeOut);
                }
            }
            yield return env.Delay((layer.Size - lastSpikingNeuron) * delayModel.ComputeTime);
            totalInputSpikes++;
            totalOutputSpikes += nrOutputSpikes;

            OnSyncEnded?.Invoke(env.Now, sync.TS, layer);
        }
    }

    public override string ToString()
    {
        return $"{this.Name}";
    }

    string Core.Name() => this.Name;
}



