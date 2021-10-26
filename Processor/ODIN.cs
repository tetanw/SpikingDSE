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


        public SpikeSourceTrace(string path, long startTime = 0, SpikeSourceTraceReporter reporter = null)
        {
            this.path = path;
            this.startTime = startTime;
            this.reporter = reporter;
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

        public override IEnumerable<Command> Run()
        {
            yield return env.SleepUntil(startTime);
            foreach (var (neuron, time) in EventTraceReader.ReadInputs(path, 100_000_000, startTime))
            {
                yield return env.Send(spikesOut, neuron);
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

        public SpikeSink(SpikeSinkReporter reporter = null)
        {
            this.reporter = reporter;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                var rcv = env.Receive(spikesIn);
                yield return rcv;
                int neuron = (int)rcv.Message;
                reporter?.SpikeReceived(this, neuron, env.Now);
            }
        }
    }

    public class ODINCore : Actor
    {
        public InPort spikesIn;
        public OutPort spikesOut;

        private int src = -1;
        private int coreID;
        private int nrNeurons;
        private double threshold;
        private double[,] weights;
        private double[] pots;
        private int synComputeTime;
        private int inputTime;
        private int outputTime;

        public ODINCore(int coreID, int nrNeurons, string name = "", double[,] weights = null, double threshold = 0.1, int synComputeTime = 0, int outputTime = 0, int inputTime = 0)
        {
            this.coreID = coreID;
            this.Name = name;
            if (weights == null)
            {
                this.weights = new double[nrNeurons, nrNeurons];
            }
            else
            {
                this.weights = weights;
            }
            this.pots = new double[nrNeurons];
            this.nrNeurons = nrNeurons;
            this.threshold = threshold;
            this.synComputeTime = synComputeTime;
            this.outputTime = outputTime;
            this.inputTime = inputTime;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                #region Receive()
                foreach (var cmd in Receive())
                {
                    yield return cmd;
                }
                #endregion
                #region Compute()
                foreach (var cmd in Compute())
                {
                    yield return cmd;
                }
                #endregion
            }
        }

        private bool SpikeReady()
        {
            // TODO: What needs to happen with this???
            return false;
        }

        private IEnumerable<Command> Compute()
        {
            long startNow = env.Now;
            long now = startNow;
            for (int dst = 0; dst < nrNeurons; dst++)
            {
                pots[dst] += weights[src, dst];
                now += synComputeTime;
                if (pots[dst] >= threshold)
                {
                    pots[dst] = 0.0;
                    yield return env.SendAt(spikesOut, dst, now);
                    now += outputTime;
                }
            }
            src = -1;
            yield return env.SleepUntil(now);
        }

        private IEnumerable<Command> Receive()
        {
            var rcv = env.Receive(spikesIn, waitBefore: inputTime);
            yield return rcv;
            var spike = (int)rcv.Message;
            src = spike;
        }
    }
}