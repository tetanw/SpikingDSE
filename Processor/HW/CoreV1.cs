using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public sealed class CoreV1 : Actor, Core
{
    public delegate void SpikeReceived(long time, Layer layer, int neuron, bool feedback, SpikeEvent spike, int nrHopsTravelled);
    public delegate void SpikeSent(long time, Layer from, int neuron, SpikeEvent spike);
    public delegate void SyncStarted(long time, int ts, HiddenLayer layer);
    public delegate void SyncEnded(long time, int ts, HiddenLayer layer);
    public delegate void SpikeComputed(long time, SpikeEvent spike);

    // Stats
    public long lastSpike = 0;
    public long lastSync = 0;
    public int nrSpikesProduced = 0;
    public int nrSpikesConsumed = 0;
    public int nrSOPs = 0;
    public int nrSpikesDroppedCore = 0;
    public int nrSpikesDroppedInput = 0;
    public int nrLateSpikes = 0;
    public double energySpent = 0.0;

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public SyncStarted OnSyncStarted;
    public SyncEnded OnSyncEnded;
    public SpikeComputed OnSpikeComputed;

    public InPort input = new InPort();
    public OutPort output = new OutPort();

    private MeshCoord thisLoc;
    private MappingTable mapping;
    private Buffer<CoreEvent> coreBuffer;
    private Buffer<CoreEvent> inputBuffer;
    private CoreV1Spec spec;

    public CoreV1(MeshCoord location, CoreV1Spec spec)
    {
        this.thisLoc = location;
        this.spec = spec;
        this.Name = spec.Name;
    }

    public object GetLocation() => thisLoc;

    public void LoadMapping(MappingTable mapping) => this.mapping = mapping;

    private IEnumerable<Event> Receiver(Simulator env)
    {
        int TS = 0;

        while (true)
        {
            var rcv = env.Receive(input, transferTime: spec.InputDelay);
            yield return rcv;
            var packet = (MeshPacket)rcv.Message;
            var @event = packet.Message as CoreEvent;

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
                        else
                            nrSpikesDroppedCore++;
                    }

                    TS = sync.TS + 1;
                    break;
                case SpikeEvent spike:
                    spike.ReceivedAt = env.Now;
                    OnSpikeReceived?.Invoke(env.Now, spike.Layer, spike.Neuron, spike.Feedback, spike, packet.NrHops);

                    if (spike.TS > TS)
                    {
                        if (!inputBuffer.IsFull)
                            inputBuffer.Push(spike);
                        else
                            nrSpikesDroppedInput++;
                    }
                    else if (spike.TS == TS)
                    {
                        if (!coreBuffer.IsFull)
                            coreBuffer.Push(spike);
                        else
                            nrSpikesDroppedCore++;
                    }
                    else
                    {
                        nrLateSpikes++;
                    }
                    break;
                default:
                    throw new Exception("Unknown event when handling input: " + @event);
            }


        }
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        inputBuffer = new(env, spec.BufferSize);
        coreBuffer = new(env, spec.BufferSize);
        env.Process(Receiver(env));

        while (true)
        {
            yield return coreBuffer.RequestRead();
            var core = coreBuffer.Read();
            coreBuffer.ReleaseRead();

            switch (core)
            {
                case SyncEvent sync:
                    foreach (var ev in Sync(env, sync))
                        yield return ev;
                    lastSync = env.Now;
                    break;
                case SpikeEvent spike:
                    foreach (var ev in Compute(env, spike))
                        yield return env.Process(Compute(env, spike));
                    lastSpike = env.Now;
                    break;
                default:
                    throw new Exception("Unknown event!");
            }
        }
    }

    private IEnumerable<Event> Compute(Simulator env, SpikeEvent spike)
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
        nrSpikesConsumed++;
        nrSOPs += spike.Layer.Size;
        energySpent += spec.ComputeEnergy;
        yield return env.Delay(spec.ComputeDelay * spike.Layer.Size);
    }

    private IEnumerable<Event> Sync(Simulator env, SyncEvent sync)
    {
        nrSpikesProduced = 0;
        nrSpikesConsumed = 0;
        nrSOPs = 0;

        var mappedLayers = mapping.LayersOf(this);
        foreach (var l in mappedLayers)
        {
            long startTime = env.Now;
            var layer = (HiddenLayer)l;

            // Readout of timestep TS - 1
            OnSyncStarted?.Invoke(env.Now, sync.TS, layer);

            // Threshold of timestep TS - 1
            int lastSpikingNeuron = 0;
            foreach (var spikingNeuron in layer.Sync())
            {
                // Delay accounting
                var neuronsComputed = spikingNeuron - lastSpikingNeuron;
                yield return env.Delay(spec.ComputeDelay * neuronsComputed);
                long afterDelayTime = env.Now;
                lastSpikingNeuron = spikingNeuron;

                // Stats accounting
                nrSpikesProduced++;

                // Feedback spikes
                int offset = (layer as ALIFLayer).Offset;
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
                        var flit = new MeshPacket
                        {
                            Src = thisLoc,
                            Dest = siblingCoord,
                            Message = spikeEv
                        };
                        yield return env.Delay(spec.OutputDelay);
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
                        else
                            nrSpikesDroppedCore++;
                    }
                    else
                    {
                        var flit = new MeshPacket
                        {
                            Src = thisLoc,
                            Dest = destCoord,
                            Message = spikeOut
                        };
                        yield return env.Delay(spec.OutputDelay);
                        yield return env.Send(output, flit);
                    }
                    OnSpikeSent?.Invoke(env.Now, layer, spikingNeuron, spikeOut);
                }
            }
            yield return env.Delay((layer.Size - lastSpikingNeuron) * spec.ComputeDelay);

            OnSyncEnded?.Invoke(env.Now, sync.TS, layer);
        }
    }

    public override string ToString()
    {
        return $"{this.Name}";
    }

    string Core.Name() => this.Name;
}
