using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public sealed class CoreV1 : Core
{
    record struct ComputeElement(bool IsLast, SpikeEvent Spike);

    public delegate void SpikeReceived(long time, Layer layer, int neuron, bool feedback, SpikeEvent spike, int nrHopsTravelled);
    public delegate void SpikeSent(long time, Layer from, int neuron);
    public delegate void SyncStarted(long time, int ts, HiddenLayer layer);
    public delegate void SyncEnded(long time, int ts, HiddenLayer layer);
    public delegate void SpikeComputed(long time, SpikeEvent spike);

    // Stats
    public int nrSOPs = 0;
    public int nrNOPs = 0;
    public int nrSpikesGenerated = 0;
    public long receiverBusy;
    public long ALUBusy;
    public long senderBusy;

    // Memory stats
    public int layerReads, layerWrites = 0;
    public int neuronReads, neuronWrites = 0;
    public int synapseReads, synapseWrites = 0;
    public int computePushes, computePops = 0;
    public int outputPushes, outputPops = 0;

    // Layer update & integrate stats
    private Dictionary<string, int> layerIntegrates = new();
    private Dictionary<string, int> layerSyncs = new();

    public SpikeReceived OnSpikeReceived;
    public SpikeSent OnSpikeSent;
    public SyncStarted OnSyncStarted;
    public SyncEnded OnSyncEnded;
    public SpikeComputed OnSpikeComputed;

    private readonly CoreV1Spec spec;
    private Buffer<Packet> outputBuffer;
    private Queue<ComputeElement> computeBuffer;
    private Buffer<SyncEvent> syncs;
    private int nrFaultySpikes = 0;

    public CoreV1(CoreV1Spec spec)
    {
        Name = spec.Name;
        this.spec = spec;
    }

    private IEnumerable<Event> Receiver(Simulator env)
    {
        int TS = 0;

        while (true)
        {
            // Delay is determined by the NoC by setting a transfer time on the send side
            var rcv = env.Receive(Input);
            yield return rcv;
            var packet = (Packet)rcv.Message;
            var @event = packet.Message as CoreEvent;
            receiverBusy += env.Now - rcv.StartedReceiving;

            if (@event is SyncEvent sync)
            {
                yield return env.Delay(spec.ReceiveSyncLat);
                TS = sync.TS + 1;
                computePushes++;
                computeBuffer.Enqueue(new ComputeElement(true, null));
                syncs.Push(sync);
            }
            else if (@event is SpikeEvent spike)
            {
                yield return env.Delay(spec.ReceiveSpikeLat);
                spike.ReceivedAt = env.Now;
                OnSpikeReceived?.Invoke(env.Now, spike.Layer, spike.Neuron, spike.Feedback, spike, packet.NrHops);


                computePushes++;
                computeBuffer.Enqueue(new(false, spike));
            }
        }
    }

    private IEnumerable<Event> ALU(Simulator env)
    {
        int TS = 0;

        while (true)
        {
            yield return syncs.RequestRead();
            var sync = syncs.Read();
            syncs.ReleaseRead();
            long before = env.Now;

            // Integrate all synapses
            while (true)
            {
                yield return env.Delay(spec.ALUReadLat);
                var (isDone, spike) = computeBuffer.Dequeue();
                computePops++;
                if (isDone)
                    break;
                if (spike.TS != TS)
                    nrFaultySpikes++;
                foreach (var ev in Compute(env, spike))
                    yield return ev;
            }

            // Sync all neurons
            foreach (var ev in Sync(env, sync))
                yield return ev;

            // Report done if needed
            if (spec.ReportSyncEnd)
            {
                yield return env.Delay(spec.ALUWriteLat);
                yield return outputBuffer.RequestWrite();
                outputBuffer.Write(new Packet
                {
                    Dest = Mapping.ControllerCoord,
                    Src = Location,
                    Message = new ReadyEvent
                    {
                        TS = sync.TS,
                        Core = this
                    }
                });
                outputBuffer.ReleaseWrite();
                outputPushes++;
            }

            ALUBusy += env.Now - before;
            TS++;
        }
    }

    private IEnumerable<Event> Sender(Simulator env)
    {
        while (true)
        {
            yield return outputBuffer.RequestRead();
            long before = env.Now;
            var packet = outputBuffer.Read();
            outputPops++;
            yield return env.Send(Output, packet);
            outputBuffer.ReleaseRead();
            senderBusy += env.Now - before;
        }
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        bool isEmpty = Mapping.GetAllLayers(this).Count == 0;
        if (isEmpty && spec.DisableIfIdle)
            yield break;

        outputBuffer = new(env, spec.OutputBufferDepth);
        computeBuffer = new();
        syncs = new(env, 1);
        env.Process(Receiver(env));
        env.Process(ALU(env));
        env.Process(Sender(env));
    }

    private IEnumerable<Event> Compute(Simulator env, SpikeEvent spike)
    {
        OnSpikeComputed?.Invoke(env.Now, spike);
        var layer = spike.Layer as HiddenLayer;
        if (spike.Feedback)
            layer.Feedback(spike.Neuron);
        else
            layer.Forward(spike.Neuron);
        layerIntegrates.AddCount(layer.TypeName, layer.Size);
        nrSOPs += layer.Size;
        layerReads++;
        neuronReads += layer.Size;
        neuronWrites += layer.Size;
        synapseReads += layer.Size;
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
            var destCoord = Mapping.CoordOf(destLayer);

            yield return env.Delay(spec.ALUWriteLat);
            yield return outputBuffer.RequestWrite();
            var flit = new Packet
            {
                Src = Location,
                Dest = destCoord,
                Message = spikeOut
            };
            outputPushes++;
            outputBuffer.Write(flit);
            outputBuffer.ReleaseWrite();
        }
    }

    private IEnumerable<Event> SendPendingSpikes(Simulator env, int TS, List<(HiddenLayer, int)> pendingSpikes)
    {
        foreach (var (layer, neuron) in pendingSpikes)
        {
            // Recurrent spikes
            if (layer.Recurrent)
                foreach (var ev in SendSpikes(env, Mapping.GetSiblings(layer).Cast<HiddenLayer>(), true, TS, layer.Offset() + neuron))
                    yield return ev;

            // Forward spikes
            foreach (var ev in SendSpikes(env, Mapping.GetDestLayers(layer).Cast<HiddenLayer>(), false, TS, layer.Offset() + neuron))
                yield return ev;

            OnSpikeSent?.Invoke(env.Now, layer, neuron);
        }

        // No more pending spikes they are all sent
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
                nrSpikesGenerated += pendingSpikes.Count;
                foreach (var ev in SendPendingSpikes(env, sync.TS, pendingSpikes))
                    yield return ev;
                nrSpikesProcessed = 0;
            }
            
            layerSyncs.AddCount(layer.TypeName, layer.Size);
            layerReads++;
            nrNOPs += layer.Size;
            neuronReads += layer.Size;
            neuronWrites += layer.Size;
            layer.FinishSync();
            OnSyncEnded?.Invoke(env.Now, sync.TS, layer);
        }
    }

    public override string[] Report(long now, bool header)
    {
        // Cores that do not have any layers should just stay silent
        if (Mapping.GetAllLayers(this).Count == 0)
            return Array.Empty<string>();

        var masterCounter = OpCounter.Merge(Mapping.GetAllLayers(this).Select(l => (l as HiddenLayer).Ops));
        var cols = new List<string>();
        if (header)
        {
            cols.Add($"{Name}_sops");
            cols.Add($"{Name}_faultySpikes");
            cols.Add($"{Name}_sparsity");
            cols.Add($"{Name}_alu_util");
            cols.Add($"{Name}_recv_util");
            cols.Add($"{Name}_snd_util");

            if (spec.ShowLayerStats)
            {
                var layers = layerSyncs.Keys.SelectMany((layer) => new string[] { $"{Name}_{layer}_integrates", $"{Name}_{layer}_syncs" });
                cols.AddRange(layers);
            }

            if (spec.ShowMemStats)
            {
                cols.Add($"{Name}_layerReads");
                cols.Add($"{Name}_layerWrites");
                cols.Add($"{Name}_neuronReads");
                cols.Add($"{Name}_neuronWrites");
                cols.Add($"{Name}_synapseReads");
                cols.Add($"{Name}_synapseWrites");
                cols.Add($"{Name}_computePushes");
                cols.Add($"{Name}_computePops");
                cols.Add($"{Name}_outputPushes");
                cols.Add($"{Name}_outputPops");
            }

            if (spec.ShowALUStats)
            {
               cols.AddRange(masterCounter.AllCounts().Select((p) => $"{Name}_ops_{p.name}"));
            }

        }
        else
        {
            double aluUtil = (double)ALUBusy / now;
            double recvUtil = (double)receiverBusy / now;
            double sndUtil = (double) senderBusy / now;
            double sparsity = (double) nrSpikesGenerated / nrNOPs;

            cols.Add($"{nrSOPs}");
            cols.Add($"{nrFaultySpikes}");
            cols.Add($"{sparsity}");
            cols.Add($"{aluUtil}");
            cols.Add($"{recvUtil}");
            cols.Add($"{sndUtil}");

            if (spec.ShowLayerStats)
            {
                foreach (var name in layerSyncs.Keys)
                {
                    cols.Add(layerIntegrates[name].ToString());
                    cols.Add(layerSyncs[name].ToString());
                }
            }

            if (spec.ShowMemStats)
            {
                cols.Add($"{layerReads}");
                cols.Add($"{layerWrites}");
                cols.Add($"{neuronReads}");
                cols.Add($"{neuronWrites}");
                cols.Add($"{synapseReads}");
                cols.Add($"{synapseWrites}");
                cols.Add($"{computePushes}");
                cols.Add($"{computePops}");
                cols.Add($"{outputPushes}");
                cols.Add($"{outputPops}");
            }

            if (spec.ShowALUStats)
            {
                cols.AddRange(masterCounter.AllCounts().Select((p) => $"{p.amount}"));
            }
        }

        return cols.ToArray();
    }
}
