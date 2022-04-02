using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public sealed class CoreV1 : Actor, ICore
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
    public int nrSpikesReceived = 0;

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public SyncStarted OnSyncStarted;
    public SyncEnded OnSyncEnded;
    public SpikeComputed OnSpikeComputed;

    public InPort input = new();
    public OutPort output = new();

    private readonly CoreV1Spec spec;
    private readonly object loc;
    private MappingTable mapping;
    private Buffer<CoreEvent> coreBuffer;
    private Buffer<CoreEvent> inputBuffer;

    public CoreV1(object location, CoreV1Spec spec)
    {
        this.loc = location;
        this.spec = spec;
        this.Name = spec.Name;
    }

    public object GetLocation() => loc;

    public void LoadMapping(MappingTable mapping) => this.mapping = mapping;

    private IEnumerable<Event> Receiver(Simulator env)
    {
        int TS = 0;

        while (true)
        {
            var rcv = env.Receive(input, transferTime: spec.InputDelay);
            yield return rcv;
            var packet = (Packet)rcv.Message;
            var @event = packet.Message as CoreEvent;

            if (@event is SyncEvent sync)
            {
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
            }
            else if (@event is SpikeEvent spike)
            {
                spike.ReceivedAt = env.Now;
                nrSpikesReceived++;
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
            }
        }
    }

    private IEnumerable<Event> ALU(Simulator env)
    {
        while (true)
        {
            yield return coreBuffer.RequestRead();
            var @event = coreBuffer.Read();
            coreBuffer.ReleaseRead();

            if (@event is SyncEvent sync)
            {
                foreach (var ev in Sync(env, sync))
                    yield return ev;
                lastSync = env.Now;
            }
            else if (@event is SpikeEvent spike)
            {
                foreach (var ev in Compute(env, spike))
                    yield return ev;
                lastSpike = env.Now;
            }
            else
                throw new Exception("Unknown event!");
        }
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        inputBuffer = new(env, spec.BufferSize);
        coreBuffer = new(env, spec.BufferSize);
        env.Process(Receiver(env));
        env.Process(ALU(env));
        yield break;
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

    private IEnumerable<Event> SendFeedbackSpikes(Simulator env, HiddenLayer layer, SyncEvent sync, int spikingNeuron)
    {
        foreach (var sibling in mapping.GetSiblings(layer))
        {
            var spikeEv = new SpikeEvent()
            {
                Layer = sibling,
                Neuron = spikingNeuron + layer.Offset(),
                Feedback = true,
                TS = sync.TS + 1,
                CreatedAt = env.Now
            };
            var siblingCoord = mapping.CoordOf(sibling);
            if (siblingCoord == loc)
            {
                if (!coreBuffer.IsFull)
                {
                    spikeEv.ReceivedAt = env.Now;
                    coreBuffer.Push(spikeEv);
                }
            }
            else
            {
                // Send recurrent spikes to other core
                var flit = new Packet
                {
                    Src = loc,
                    Dest = siblingCoord,
                    Message = spikeEv
                };
                yield return env.Send(output, flit, transferTime: spec.OutputDelay);
            }
        }
    }

    private IEnumerable<Event> SendOutputSpikes(Simulator env, HiddenLayer layer, SyncEvent sync, int spikingNeuron)
    {
        foreach (var destLayer in mapping.GetDestLayers(layer))
        {
            var spikeOut = new SpikeEvent()
            {
                Layer = destLayer,
                Neuron = spikingNeuron + layer.Offset(),
                Feedback = false,
                TS = sync.TS + 1,
                CreatedAt = env.Now
            };
            var destCoord = mapping.CoordOf(destLayer);
            if (destCoord == loc)
            {
                spikeOut.ReceivedAt = env.Now;
                if (!coreBuffer.IsFull)
                    coreBuffer.Push(spikeOut);
                else
                    nrSpikesDroppedCore++;
            }
            else
            {
                var flit = new Packet
                {
                    Src = loc,
                    Dest = destCoord,
                    Message = spikeOut
                };
                yield return env.Send(output, flit, transferTime: spec.OutputDelay);
            }
            OnSpikeSent?.Invoke(env.Now, layer, spikingNeuron, spikeOut);
        }
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
            for (int i = 0; i < layer.Size; i++)
            {
                if (!layer.Sync(i))
                    continue;
                
                // Delay accounting
                var neuronsComputed = i - lastSpikingNeuron;
                yield return env.Delay(spec.ComputeDelay * neuronsComputed);
                long afterDelayTime = env.Now;
                lastSpikingNeuron = i;

                // Stats accounting
                nrSpikesProduced++;

                // Feedback spikes
                if (layer.IsRecurrent())
                    foreach (var ev in SendFeedbackSpikes(env, layer, sync, i))
                        yield return ev;

                // Forward spikes
                foreach (var ev in SendOutputSpikes(env, layer, sync, i))
                    yield return ev;
            }
            yield return env.Delay((layer.Size - lastSpikingNeuron) * spec.ComputeDelay);
            layer.FinishSync();

            OnSyncEnded?.Invoke(env.Now, sync.TS, layer);
        }
    }

    public override string ToString()
    {
        return $"{this.Name}";
    }

    string ICore.Name() => this.Name;

    public OutPort Output() => output;

    public InPort Input() => input;
}
