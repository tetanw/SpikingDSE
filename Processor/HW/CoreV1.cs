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

    public void AddLayer(Layer layer) {}

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
                        var spike = inputBuffer.Pop();
                        if (!coreBuffer.IsFull)
                            coreBuffer.Push(spike);
                    }

                    TS = sync.TS + 1;
                    break;
                case SpikeEvent spike:
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
                    yield return env.Process(Sync(env, sync.TS));
                    break;
                case SpikeEvent spike:
                    if (spike.Feedback)
                    {
                        yield return env.Process(Feedback(env, spike.Layer, spike.Neuron));
                    }
                    else
                    {
                        yield return env.Process(Compute(env, spike.Layer, spike.Neuron));
                    }
                    break;
                default:
                    throw new Exception("Unknown event!");
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

    private IEnumerable<Event> Sync(Environment env, int TS)
    {
        totalOutputSpikes = 0;
        totalInputSpikes = 0;
        foreach (var l in mapping[this])
        {
            var layer = (HiddenLayer) l;

            // Readout of timestep TS - 1
            OnSyncStarted?.Invoke(this, env.Now, TS, layer);

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
                    // TODO: CreatedAt
                    var spikeEv = new SpikeEvent()
                    {
                        Layer = sibling,
                        Neuron = spikingNeuron + offset,
                        Feedback = true,
                        TS = TS + 1
                    };
                    var siblingCoord = mapping.CoordOf(sibling);
                    if (siblingCoord == thisLoc)
                    {
                        if (layer is ALIFLayer && !coreBuffer.IsFull)
                        {
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
                    // TODO: CreatedAt
                    var spikeEv = new SpikeEvent()
                    {
                        Layer = destLayer,
                        Neuron = spikingNeuron + offset,
                        Feedback = false,
                        TS = TS + 1
                    };
                    var destCoord = mapping.CoordOf(destLayer);
                    if (destCoord == thisLoc)
                    {
                        if (!coreBuffer.IsFull)
                            coreBuffer.Push(spikeEv);
                    }
                    else
                    {
                        var flit = new MeshFlit
                        {
                            Src = thisLoc,
                            Dest = destCoord,
                            Message = spikeEv
                        };
                        yield return env.Delay(delayModel.OutputTime);
                        yield return env.Send(output, flit);
                    }
                    OnSpikeSent?.Invoke(this, env.Now, layer, spikingNeuron);
                }
            }
            yield return env.Delay((layer.Size - lastSpikingNeuron) * delayModel.ComputeTime);
            totalInputSpikes++;
            totalOutputSpikes += nrOutputSpikes;

            OnSyncEnded?.Invoke(this, env.Now, TS, layer);
        }
    }

    public override string ToString()
    {
        return $"{this.Name}";
    }

    string Core.Name() => this.Name;
}



