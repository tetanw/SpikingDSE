using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE
{
    public interface SpikeSourceTraceReporter
    {
        public void SpikeSent(SpikeSourceTrace source, int neuron, long time);
    }

    public class SpikeSourceTrace : Actor, Source
    {
        public OutPort spikesOut = new OutPort();

        private InputLayer inputLayer;
        private long startTime;
        private SpikeSourceTraceReporter reporter;
        private Func<int, object> outTransformer;

        public SpikeSourceTrace(long startTime = 0, SpikeSourceTraceReporter reporter = null, string name = null)
        {
            this.Name = name;
            this.startTime = startTime;
            this.reporter = reporter;
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
                reporter?.SpikeSent(this, neuron, env.Now);
            }
        }
    }

    public interface SpikeSinkReporter
    {
        public void SpikeReceived(SpikeSink sink, int neuron, long time);
    }

    public class SpikeSink : Actor, Sink
    {
        public InPort spikesIn = new InPort();

        private SpikeSinkReporter reporter;
        private Func<object, int> inTransformer;

        public SpikeSink(SpikeSinkReporter reporter = null, Func<object, int> inTransformer = null, string name = null)
        {
            this.Name = name;
            this.reporter = reporter;
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
                reporter?.SpikeReceived(this, neuron, env.Now);
            }
        }
    }

    public interface ODINReporter
    {
        public void ReceivedSpike(ODINCore core, long time, int neuron);
        public void ProducedSpike(ODINCore core, long time, int neuron);
    }

    public struct ODINDelayModel
    {
        public int InputTime;
        public int ComputeTime;
        public int OutputTime;
    }

    public class ODINCore : Actor, Core
    {
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
        private ODINReporter reporter;

        public ODINCore(int nrNeurons, ODINDelayModel delayModel, string name = "", ODINReporter reporter = null)
        {
            this.Name = name;
            this.nrNeurons = nrNeurons;
            this.delayModel = delayModel;
            this.reporter = reporter;
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
                yield return env.Process(Receive(env));
                yield return env.Process(Compute(env));
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
                    reporter?.ProducedSpike(this, env.Now, dst);
                    int neuron = dst + layer.baseID;
                    var message = outTransformer == null ? neuron : outTransformer(neuron);
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
            var spike = inTransformer == null ? (int)rcv.Message : inTransformer(rcv.Message);
            src = spike;
            reporter?.ReceivedSpike(this, env.Now, spike);
        }
    }
}