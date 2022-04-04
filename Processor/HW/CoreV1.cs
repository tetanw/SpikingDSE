using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public sealed class CoreV1 : Actor, ICore
{
    public delegate void SpikeReceived(long time, Layer layer, int neuron, bool feedback, SpikeEvent spike, int nrHopsTravelled);
    public delegate void SpikeSent(long time, Layer from, int neuron);
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
    private Buffer<Packet> outputBuffer;

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

    private IEnumerable<Event> Sender(Simulator env)
    {
        while (true)
        {
            yield return outputBuffer.RequestRead();
            var packet = outputBuffer.Read();
            yield return env.Send(output, packet);
            outputBuffer.ReleaseRead();
        }
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        inputBuffer = new(env, spec.ComputeBufferSize);
        coreBuffer = new(env, spec.ComputeBufferSize);
        outputBuffer = new(env, spec.OutputBufferSize);
        env.Process(Receiver(env));
        env.Process(ALU(env));
        env.Process(Sender(env));
        yield break;
    }

    private IEnumerable<Event> Compute(Simulator env, SpikeEvent spike)
    {
        OnSpikeComputed?.Invoke(env.Now, spike);
        var layer = spike.Layer as HiddenLayer;
        if (spike.Feedback)
            layer.Feedback(spike.Neuron);
        else
            layer.Forward(spike.Neuron);
        nrSpikesConsumed++;
        nrSOPs += spike.Layer.Size;
        energySpent += spec.ComputeEnergy * spike.Layer.Size;
        yield return env.Delay(spec.IntegrateDelay * spike.Layer.Size / spec.NrParallel);
    }

    public IEnumerable<Event> SendSpikes(Simulator env, IEnumerable<HiddenLayer> dests, bool feedback, int TS, int spikingNeuron)
    {
        foreach (var destLayer in dests)
        {
            var spikeOut = new SpikeEvent()
            {
                Layer = destLayer,
                Neuron = spikingNeuron,
                Feedback = feedback,
                TS = TS + 1,
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
                yield return outputBuffer.RequestWrite();
                var flit = new Packet
                {
                    Src = loc,
                    Dest = destCoord,
                    Message = spikeOut
                };
                outputBuffer.Write(flit);
                outputBuffer.ReleaseWrite();
            }
        }
    }

    private IEnumerable<Event> SendPendingSpikes(Simulator env, int TS, List<(HiddenLayer, int)> pendingSpikes)
    {
        foreach (var (layer, neuron) in pendingSpikes)
        {
            if (layer.IsRecurrent())
                foreach (var ev in SendSpikes(env, mapping.GetSiblings(layer).Cast<HiddenLayer>(), true, TS, layer.Offset() + neuron))
                    yield return ev;

            // Forward spikes
            foreach (var ev in SendSpikes(env, mapping.GetDestLayers(layer).Cast<HiddenLayer>(), false, TS, layer.Offset() + neuron))
                yield return ev;

            OnSpikeSent?.Invoke(env.Now, layer, neuron);
        }

        pendingSpikes.Clear();
    }

    private IEnumerable<Event> Sync(Simulator env, SyncEvent sync)
    {
        var mappedLayers = mapping.LayersOf(this).Cast<HiddenLayer>();
        var pendingSpikes = new List<(HiddenLayer, int)>();
        int nrSpikesProcessed = 0;
        foreach (var layer in mappedLayers)
        {
            OnSyncStarted?.Invoke(env.Now, sync.TS, layer);

            for (int line = 0; line < layer.Size; line += spec.NrParallel)
            {
                int spikesLeft = spec.NrParallel - nrSpikesProcessed;
                for (int neuron = line; neuron < Math.Min(line + spec.NrParallel, Math.Min(layer.Size, line + spikesLeft)); neuron++)
                {
                    if (layer.Sync(neuron))
                        pendingSpikes.Add((layer, neuron));
                    nrSpikesProcessed++;
                }

                if ((spec.IgnoreLayers && nrSpikesProcessed == spec.NrParallel) || !spec.IgnoreLayers)
                {
                    yield return env.Delay(spec.SyncDelay);
                    foreach (var ev in SendPendingSpikes(env, sync.TS, pendingSpikes))
                        yield return ev;
                    nrSpikesProcessed = 0;
                }
            }
            layer.FinishSync();
            OnSyncEnded?.Invoke(env.Now, sync.TS, layer);
        }

        if (nrSpikesProcessed > 0)
        {
            yield return env.Delay(spec.SyncDelay);
            foreach (var ev in SendPendingSpikes(env, sync.TS, pendingSpikes))
                yield return ev;
        }
    }

    string ICore.Name() => this.Name;

    public OutPort Output() => output;

    public InPort Input() => input;
}
