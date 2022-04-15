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
    public int nrEarlySpikes = 0;
    public double energySpent = 0.0;
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

    private Queue<SpikeEvent>[] forwardSpikes;
    private Queue<SpikeEvent>[] feedbackSpikes;
    private Signal syncSignal;
    private SyncEvent lastSyncEv;

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
            // Delay is determined by the NoC by setting a transfer time on the send side
            var rcv = env.Receive(input);
            yield return rcv;
            var packet = (Packet)rcv.Message;
            var @event = packet.Message as CoreEvent;
            receiverBusy += env.Now - rcv.StartedReceiving;

            if (@event is SyncEvent sync)
            {
                lastSyncEv = sync;
                TS = sync.TS + 1;
                syncSignal.Notify();
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
                    if (spike.Feedback)
                    {
                        feedbackSpikes[TS % 2].Enqueue(spike);
                    }
                    else
                    {
                        forwardSpikes[TS % 2].Enqueue(spike);
                    }
                }
            }
        }
    }

    private IEnumerable<Event> ALU(Simulator env)
    {
        while (true)
        {
            yield return syncSignal.Wait();
            long before = env.Now;

            int TS = lastSyncEv.TS;
            while (feedbackSpikes[TS % 2].Count > 0)
            {
                var spike = feedbackSpikes[TS % 2].Dequeue();
                foreach (var ev in Compute(env, spike))
                    yield return ev;
            }

            while (forwardSpikes[TS % 2].Count > 0)
            {
                var spike = forwardSpikes[TS % 2].Dequeue();
                foreach (var ev in Compute(env, spike))
                    yield return ev;
            }

            foreach (var ev in Sync(env, lastSyncEv))
                yield return ev;

            if (spec.DoSyncEndEvent)
            {
                yield return outputBuffer.RequestWrite();
                outputBuffer.Write(new Packet {
                    Dest = mapping.ControllerCoord,
                    Src = loc,
                    Message = new SyncDone {
                        TS = TS,
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
        outputBuffer = new(env, spec.OutputBufferSize);
        forwardSpikes = new Queue<SpikeEvent>[2];
        forwardSpikes[0] = new();
        forwardSpikes[1] = new();
        feedbackSpikes = new Queue<SpikeEvent>[2];
        feedbackSpikes[0] = new();
        feedbackSpikes[1] = new();
        syncSignal = new(env);
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
        // Calculate amount of lines required, careful: trick to ceil divide
        int nrLines = (spike.Layer.Size + spec.NrParallel) / spec.NrParallel;
        yield return env.Delay(spec.IntegrateDelay * nrLines);
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
        var pendingSpikes = new List<(HiddenLayer, int)>();
        int nrSpikesProcessed = 0;
        foreach (var layer in sync.Layers.Cast<HiddenLayer>())
        {
            OnSyncStarted?.Invoke(env.Now, sync.TS, layer);

            for (int line = 0; line < layer.Size; line += spec.NrParallel)
            {
                int spikesLeft = spec.NrParallel - nrSpikesProcessed;
                for (int neuron = line; neuron < Math.Min(line + spec.NrParallel, layer.Size); neuron++)
                {
                    if (layer.Sync(neuron))
                        pendingSpikes.Add((layer, neuron));
                    nrSpikesProcessed++;
                }

                yield return env.Delay(spec.SyncDelay);
                foreach (var ev in SendPendingSpikes(env, sync.TS, pendingSpikes))
                    yield return ev;
                nrSpikesProcessed = 0;
            }
            layer.FinishSync();
            OnSyncEnded?.Invoke(env.Now, sync.TS, layer);
        }
    }

    string ICore.Name() => this.Name;

    public OutPort Output() => output;

    public InPort Input() => input;
}
