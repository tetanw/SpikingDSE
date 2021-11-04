using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE
{
    public delegate void SpikeSent(SpikeSourceTrace source, int neuron, long time);

    public sealed class SpikeSourceTrace : Actor, Source
    {
        public SpikeSent SpikeSent;

        public OutPort spikesOut = new OutPort();

        private InputLayer inputLayer;
        private long startTime;
        private Func<int, object> outTransformer;

        public SpikeSourceTrace(long startTime = 0, string name = null)
        {
            this.Name = name;
            this.startTime = startTime;
        }

        public OutPort GetOut()
        {
            return spikesOut;
        }

        public void LoadLayer(InputLayer inputLayer)
        {
            this.inputLayer = inputLayer;
        }

        public void LoadOutTransformer(Func<int, object> outTransformer)
        {
            this.outTransformer = outTransformer;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            yield return env.SleepUntil(startTime);
            foreach (var neuron in inputLayer.inputSpikes)
            {
                var message = outTransformer == null ? neuron : outTransformer(neuron);
                yield return env.Send(spikesOut, message);
                SpikeSent?.Invoke(this, neuron, env.Now);
            }
        }
    }

    public delegate void SpikeReceived(SpikeSink sink, int neuron, long time);

    public sealed class SpikeSink : Actor, Sink
    {
        public SpikeReceived SpikeReceived;

        public InPort spikesIn = new InPort();

        private Func<object, int> inTransformer;

        public SpikeSink(Func<object, int> inTransformer = null, string name = null)
        {
            this.Name = name;
            this.inTransformer = inTransformer;
        }

        public InPort GetIn()
        {
            return spikesIn;
        }

        public void LoadInTransformer(Func<object, int> inTransformer)
        {
            this.inTransformer = inTransformer;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            while (true)
            {
                var rcv = env.Receive(spikesIn);
                yield return rcv;
                int neuron = inTransformer == null ? (int)rcv.Message : inTransformer(rcv.Message);
                SpikeReceived?.Invoke(this, neuron, env.Now);
            }
        }
    }

    public delegate void ReceivedSpike(ODINCore core, long time, int neuron);
    public delegate void ProducedSpike(ODINCore core, long time, int neuron);

    public struct ODINDelayModel
    {
        public int InputTime;
        public int ComputeTime;
        public int OutputTime;
    }

    public sealed class ODINCore : Actor, Core
    {
        public ProducedSpike ProducedSpike;
        public ReceivedSpike ReceivedSpike;

        public InPort spikesIn = new InPort();
        public OutPort spikesOut = new OutPort();

        // TODO: Rather not pass between and receive by using this variable
        private int src = -1;
        private ODINLayer layer;
        private ODINDelayModel delayModel;
        private int nrNeurons;
        private int nrNeuronsFilled = 0;
        private Func<int, object> outTransformer;
        private Func<object, int> inTransformer;

        public ODINCore(int nrNeurons, ODINDelayModel delayModel, string name = "")
        {
            this.Name = name;
            this.nrNeurons = nrNeurons;
            this.delayModel = delayModel;
        }

        public bool AcceptsLayer(Layer layer)
        {
            if (!(layer is ODINLayer))
            {
                return false;
            }
            var odinLayer = (ODINLayer)layer;
            if (nrNeuronsFilled + odinLayer.Size > nrNeurons)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public InPort GetIn()
        {
            return spikesIn;
        }

        public OutPort GetOut()
        {
            return spikesOut;
        }

        public void LoadInTransformer(Func<object, int> inTransformer)
        {
            this.inTransformer = inTransformer;
        }

        public void AddLayer(Layer layer)
        {
            if (this.layer != null)
                throw new Exception("Only accepts 1 layer");

            this.layer = (ODINLayer)layer;
            nrNeuronsFilled += this.layer.Size;
        }

        public void LoadOutTransformer(Func<int, object> outTransformer)
        {
            this.outTransformer = outTransformer;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            while (true)
            {
                foreach (var ev in Receive(env))
                {
                    yield return ev;
                }
                foreach (var ev in Compute(env))
                {
                    yield return ev;
                }
            }
        }

        private IEnumerable<Event> Compute(Environment env)
        {
            long startNow = env.Now;
            long now = startNow;
            for (int dst = 0; dst < nrNeurons; dst++)
            {
                layer.pots[dst] += layer.weights[src, dst];
                now += delayModel.ComputeTime;
                if (layer.pots[dst] >= layer.threshold)
                {
                    layer.pots[dst] = 0;
                    ProducedSpike?.Invoke(this, env.Now, dst);
                    int neuron = dst + layer.NeuronRange.Start;
                    var message = outTransformer?.Invoke(neuron) ?? neuron;
                    yield return env.SendAt(spikesOut, message, now);
                    now += delayModel.OutputTime;
                }
            }
            src = -1;
            yield return env.SleepUntil(now);
        }

        private IEnumerable<Event> Receive(Environment env)
        {
            var rcv = env.Receive(spikesIn, waitBefore: delayModel.InputTime);
            yield return rcv;
            var spike = inTransformer?.Invoke(rcv.Message) ?? (int)rcv.Message;
            src = spike - layer.InputRange.Start;
            ReceivedSpike?.Invoke(this, env.Now, spike);
        }
    }
}