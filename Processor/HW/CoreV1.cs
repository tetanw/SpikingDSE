using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public sealed class CoreV1 : Actor, ICore
{
    record struct ComputeElement(bool IsLast, SpikeEvent Spike);

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
    public int nrEarlySpikes = 0;
    public int nrSpikesReceived = 0;
    public long receiverBusy;
    public long ALUBusy;
    public long senderBusy;

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
    private Buffer<Packet> outputBuffer;

    private Queue<ComputeElement> computeBuffer;
    private Buffer<SyncEvent> syncs;

    public CoreV1(object location, CoreV1Spec spec)
    {
        loc = location;
        Name = spec.Name;
        this.spec = spec;
    }

    public object GetLocation() => loc;

    public void LoadMapping(MappingTable mapping) => this.mapping = mapping;

    private IEnumerable<Event> Receiver(Simulator env)
    {
        int TS = 0;

        while (true)
        {
            // Delay is determined by the NoC by setting a transfer time on the send side
            var rcv = env.Receive(input);
            yield return rcv;
            var packet = (Packet)rcv.Message;
            var @event = packet.Message as CoreEvent;
            receiverBusy += env.Now - rcv.StartedReceiving;

            if (@event is SyncEvent sync)
            {
                TS = sync.TS + 1;
                computeBuffer.Enqueue(new ComputeElement(true, null));
                syncs.Push(sync);
            }
            else if (@event is SpikeEvent spike)
            {
                spike.ReceivedAt = env.Now;
                nrSpikesReceived++;
                OnSpikeReceived?.Invoke(env.Now, spike.Layer, spike.Neuron, spike.Feedback, spike, packet.NrHops);

                if (spike.TS > TS)
                {
                    nrEarlySpikes++;
                }
                else if (spike.TS < TS)
                {
                    nrLateSpikes++;
                }
                else
                {
                    computeBuffer.Enqueue(new(false, spike));
                }
            }
        }
    }

    private IEnumerable<Event> ALU(Simulator env)
    {
        while (true)
        {
            yield return syncs.RequestRead();
            var sync = syncs.Read();
            syncs.ReleaseRead();
            long before = env.Now;

            while (true)
            {
                var (isDone, spike) = computeBuffer.Dequeue();
                if (isDone)
                    break;
                foreach (var ev in Compute(env, spike))
                    yield return ev;
            }

            foreach (var ev in Sync(env, sync))
                yield return ev;

            if (spec.ReportSyncEnd)
            {
                yield return outputBuffer.RequestWrite();
                outputBuffer.Write(new Packet
                {
                    Dest = mapping.ControllerCoord,
                    Src = loc,
                    Message = new SyncDone
                    {
                        TS = sync.TS,
                        Core = this
                    }
                });
                outputBuffer.ReleaseWrite();
            }

            ALUBusy += env.Now - before;
        }
    }

    private IEnumerable<Event> Sender(Simulator env)
    {
        while (true)
        {
            yield return outputBuffer.RequestRead();
            long before = env.Now;
            var packet = outputBuffer.Read();
            yield return env.Send(output, packet);
            outputBuffer.ReleaseRead();
            senderBusy += env.Now - before;
        }
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        outputBuffer = new(env, spec.OutputBufferDepth);
        computeBuffer = new();
        syncs = new(env, 1);
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
        // Calculate amount of lines required, careful: trick to ceil divide
        int nrLines = MathUtils.CeilDivide(layer.Size, spec.NrParallel);
        var costs = spec.LayerCosts[spike.Layer.TypeName];
        long integrateDelay = costs.IntegrateLat + (nrLines - 1) * costs.IntegrateII;
        yield return env.Delay(integrateDelay);
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

    private IEnumerable<Event> SendPendingSpikes(Simulator env, int TS, List<(HiddenLayer, int)> pendingSpikes)
    {
        foreach (var (layer, neuron) in pendingSpikes)
        {
            if (layer.Recurrent)
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
        var pendingSpikes = new List<(HiddenLayer, int)>();
        int nrSpikesProcessed = 0;
        foreach (var layer in sync.Layers.Cast<HiddenLayer>())
        {
            OnSyncStarted?.Invoke(env.Now, sync.TS, layer);
            layer.StartSync();
            var costs = spec.LayerCosts[layer.TypeName];
            long startTime = env.Now;
            for (int line = 0; line < layer.Size; line += spec.NrParallel)
            {
                int spikesLeft = spec.NrParallel - nrSpikesProcessed;
                for (int neuron = line; neuron < Math.Min(line + spec.NrParallel, layer.Size); neuron++)
                {
                    if (layer.Sync(neuron))
                        pendingSpikes.Add((layer, neuron));
                    nrSpikesProcessed++;
                }

                // the first line comes in at latency after that every
                // initiation interval
                if (line == 0)
                    yield return env.Delay(costs.SyncLat);
                else
                    yield return env.Delay(costs.SyncII);
                foreach (var ev in SendPendingSpikes(env, sync.TS, pendingSpikes))
                    yield return ev;
                nrSpikesProcessed = 0;
            }

            layer.FinishSync();
            OnSyncEnded?.Invoke(env.Now, sync.TS, layer);
        }
    }

    string ICore.Name() => Name;

    public OutPort Output() => output;

    public InPort Input() => input;

    public double Energy(long now)
    {
        return 0.0;
    }

    public double Memory()
    {
        double layerMem = spec.MaxLayers * (spec.BaseLayerSize + spec.MaxSplits * spec.FanoutSize);
        double synMem = spec.MaxSynapses * spec.SynapseSize;
        double neuronMem = spec.MaxNeurons * spec.NeuronSize;
        double computeBuffer = spec.ComputeBufferWidth * (spec.MaxFanIn + spec.MaxNeurons + 1);
        double outputBuffer = spec.OutputBufferWidth * spec.OutputBufferDepth;

        return layerMem + synMem + neuronMem + computeBuffer + outputBuffer;
    }
}
