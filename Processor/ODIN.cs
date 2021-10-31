using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE
{
    public interface SpikeSourceTraceReporter
    {
        public void SpikeSent(SpikeSourceTrace source, int neuron, long time);
    }

    public class SpikeSourceTrace : Actor
    {
        public OutPort spikesOut;

        private string path;
        private long startTime;
        private SpikeSourceTraceReporter reporter;
        private Func<int, object> outTransformer;

        public SpikeSourceTrace(string path, long startTime = 0, SpikeSourceTraceReporter reporter = null, Func<int, object> transformOut = null)
        {
            this.path = path;
            this.startTime = startTime;
            this.reporter = reporter;
            this.outTransformer = transformOut;
        }

        private IEnumerable<(int, long)> ReadInputSpikes(string path, int frequency)
        {
            long clkPeriodPs = 1_000_000_000_000 / frequency;
            StreamReader sr = new StreamReader(path);
            sr.ReadLine();
            sr.ReadLine();
            sr.ReadLine();
            sr.ReadLine();
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                var parts = line.Split(",");
                long time = long.Parse(parts[0]) / clkPeriodPs;
                int neuron = int.Parse(parts[1]);
                yield return (neuron, time);
            }
            sr.Close();
        }

        public override IEnumerable<Event> Run()
        {
            yield return env.SleepUntil(startTime);
            foreach (var (neuron, time) in EventTraceReader.ReadInputs(path, 100_000_000, startTime))
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

    public class SpikeSink : Actor
    {
        public InPort spikesIn;

        private SpikeSinkReporter reporter;
        private Func<object, int> inTransformer;

        public SpikeSink(SpikeSinkReporter reporter = null, Func<object, int> inTransformer = null)
        {
            this.reporter = reporter;
            this.inTransformer = inTransformer;
        }

        public override IEnumerable<Event> Run()
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

    public class ODINCore : Actor
    {
        public InPort spikesIn;
        public OutPort spikesOut;

        private int src = -1;
        private int nrNeurons;
        private int threshold;
        private int[,] weights;
        private int[] pots;
        private int synComputeTime;
        private int inputTime;
        private int outputTime;
        private Func<int, object> transformOut;
        private Func<object, int> transformIn;
        private ODINReporter reporter;

        public ODINCore(int nrNeurons, string name = "", int[,] weights = null, int threshold = 0, int synComputeTime = 0, int outputTime = 0, int inputTime = 0, Func<int, object> transformOut = null, Func<object, int> transformIn = null, ODINReporter reporter = null)
        {
            this.Name = name;
            if (weights == null)
            {
                this.weights = new int[nrNeurons, nrNeurons];
            }
            else
            {
                this.weights = weights;
            }
            this.pots = new int[nrNeurons];
            this.nrNeurons = nrNeurons;
            this.threshold = threshold;
            this.synComputeTime = synComputeTime;
            this.outputTime = outputTime;
            this.inputTime = inputTime;
            this.transformOut = transformOut;
            this.transformIn = transformIn;
            this.reporter = reporter;
        }

        public override IEnumerable<Event> Run()
        {
            while (true)
            {
                yield return env.Process(Receive());
                yield return env.Process(Compute());
            }
        }

        private IEnumerable<Event> Compute()
        {
            long startNow = env.Now;
            long now = startNow;
            for (int dst = 0; dst < nrNeurons; dst++)
            {
                pots[dst] += weights[src, dst];
                now += synComputeTime;
                if (pots[dst] >= threshold)
                {
                    pots[dst] = 0;
                    reporter?.ProducedSpike(this, env.Now, dst);
                    var message = transformOut == null ? dst : transformOut(dst);
                    yield return env.SendAt(spikesOut, message, now);
                    now += outputTime;
                }
            }
            src = -1;
            yield return env.SleepUntil(now);
        }

        private IEnumerable<Event> Receive()
        {
            var rcv = env.Receive(spikesIn, waitBefore: inputTime);
            yield return rcv;
            var spike = transformIn == null ? (int)rcv.Message : transformIn(rcv.Message);
            src = spike;
            reporter?.ReceivedSpike(this, env.Now, spike);
        }
    }
}