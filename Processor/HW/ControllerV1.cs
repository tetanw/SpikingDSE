using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;


public sealed class ControllerV1 : Controller
{
    public Action<Actor, long, SpikeEvent> SpikeSent;
    public Action<Actor, long, SpikeEvent> SpikeReceived;
    public Action<Actor, long, int> TimeAdvanced;

    public ControllerV1Spec spec;

    private Signal syncSignal;

    private Queue<SpikeEvent> spikes = new();
    private int TS = 0;

    public ControllerV1(ControllerV1Spec spec)
    {
        this.spec = spec;
        Name = spec.Name;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        syncSignal = new(env);
        env.Process(Sender(env));
        env.Process(Receiver(env));

        yield break;
    }

    private IEnumerable<Event> Sender(Simulator env)
    {
        int TS = 0;
        var inputLayer = Mapping.GetInputLayer();
        var destLayers = Mapping.GetDestLayers(inputLayer);

        yield return env.SleepUntil(spec.StartTime);
        while (spikeSource.NextTimestep())
        {
            // Do spike sending
            var inputSpikes = spikeSource.NeuronSpikes();
            foreach (var neuron in inputSpikes)
            {
                foreach (var destLayer in destLayers)
                {
                    var spike = new SpikeEvent()
                    {
                        Layer = destLayer,
                        Neuron = neuron,
                        Feedback = false,
                        TS = TS,
                        CreatedAt = env.Now
                    };
                    var dest = Mapping.CoordOf(spike.Layer);
                    var packet = new Packet
                    {
                        Src = Location,
                        Dest = dest,
                        Message = spike
                    };
                    yield return env.Send(Output, packet);
                    SpikeSent?.Invoke(this, env.Now, spike);
                }
            }

            // Wait until sync
            if (spec.GlobalSync)
            {
                var nextSync = spec.StartTime + spec.Interval * (TS + 1);
                yield return env.SleepUntil(Math.Max(nextSync, env.Now));

                // Sync
                foreach (var ev in DoSync(env, TS))
                    yield return ev;
                TS++;
            }
            else
            {
                // Sync
                foreach (var ev in DoSync(env, TS))
                    yield return ev;

                yield return syncSignal.Wait();
                TS++;
            }
        }

        if (!spec.GlobalSync)
        {
            // Sync
            foreach (var ev in DoSync(env, TS))
                yield return ev;
        }
    }

    private IEnumerable<Event> DoSync(Simulator env, int TS)
    {
        SyncMyLayers();

        foreach (var core in Mapping.Cores)
        {
            if (core is ControllerV1)
                continue;

            if (spec.IgnoreIdleCores && Mapping.GetAllLayers(core).Count == 0)
                continue;

            var sync = new SyncEvent()
            {
                TS = TS,
                CreatedAt = env.Now,
                Layers = Mapping.GetAllLayers(core) ?? new List<Layer>()
            };
            var flit = new Packet
            {
                Src = Location,
                Dest = core.Location,
                Message = sync,
            };
            yield return env.Send(Output, flit);
        }

        TimeAdvanced?.Invoke(this, env.Now, TS);
    }

    private IEnumerable<Event> Receiver(Simulator env)
    {
        var coresDone = new HashSet<object>();
        int nrCores;
        if (spec.IgnoreIdleCores)
            nrCores = Mapping.Cores.Where(c => Mapping.GetAllLayers(c).Count > 0 && c is not ControllerV1).Count();
        else
            nrCores = Mapping.Cores.Count - 1;

        while (true)
        {
            var rcv = env.Receive(Input);
            yield return rcv;
            var packet = rcv.Message as Packet;

            if (packet.Message is SyncDone syncDone)
            {
                coresDone.Add(syncDone.Core);
                if (coresDone.Count == nrCores)
                {
                    yield return env.Delay(spec.SyncDelay);
                    TS++;
                    syncSignal.Notify();
                    coresDone.Clear();
                }
            }
            else if (packet.Message is SpikeEvent spike)
            {
                spikes.Enqueue(spike);
            }
        }
    }

    private void SyncMyLayers()
    {
        while (spikes.Count > 0)
        {
            var spike = spikes.Dequeue();
            var layer = (HiddenLayer)spike.Layer;
            if (spike.Feedback)
                layer.Feedback(spike.Neuron);
            else
                layer.Forward(spike.Neuron);

            if (spike.TS != TS)
                throw new Exception("Wrongly timed spike");
        }

        var myLayers = Mapping.GetAllLayers(this);
        foreach (var layer in myLayers)
        {
            if (layer is HiddenLayer hidden)
            {
                hidden.StartSync();
                for (int i = 0; i < hidden.Size; i++)
                {
                    hidden.Sync(i);
                }
                hidden.FinishSync();
            }
        }
    }
}
