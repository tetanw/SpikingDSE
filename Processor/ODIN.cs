using System.Collections.Generic;
using System.IO;

namespace SpikingDSE
{
    public class EventTraceIn : Actor
    {
        public OutPort spikesOut;
        private string path;
        private StreamWriter sw;
        private long startTime;

        public EventTraceIn(string path, StreamWriter sw, long startTime = 0)
        {
            this.path = path;
            this.sw = sw;
            this.startTime = startTime;
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
                sw.WriteLine($"0,{neuron},{env.Now}");
            }
        }
    }

    public class SpikeSink : Actor
    {
        public InPort spikesIn;

        private StreamWriter sw;

        public SpikeSink(StreamWriter sw)
        {
            this.sw = sw;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                var rcv = env.Receive(spikesIn);
                yield return rcv;
                int spike = (int)rcv.Message;
                sw.WriteLine($"1,{spike},{env.Now}");
            }
        }
    }

    public class ODINCore : Actor
    {
        public InPort spikesIn;
        public OutPort spikesOut;

        private Queue<int> buffer = new Queue<int>();
        private int bufferCap;
        private int coreID;
        private int nrNeurons;
        private double threshold;
        private double[,] weights;
        private double[] pots;
        private int synComputeTime;
        private int inputTime;
        private int outputTime;

        public ODINCore(int coreID, int nrNeurons, double[,] weights = null, int bufferCap = 1, double threshold = 0.1, int synComputeTime = 0, int outputTime = 0, int inputTime = 0)
        {
            this.coreID = coreID;
            this.bufferCap = bufferCap;
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
                if (buffer.Count == bufferCap)
                {
                    #region Compute()
                    foreach (var item in Compute())
                    {
                        yield return item;
                    }
                    #endregion
                }
                else if (buffer.Count != bufferCap && spikesIn.Ready)
                {
                    #region Receive()
                    foreach (var item in Receive())
                    {
                        yield return item;
                    }
                    #endregion
                }
                else if (buffer.Count > 0)
                {
                    #region Compute()
                    foreach (var item in Compute())
                    {
                        yield return item;
                    }
                    #endregion
                }
                else
                {
                    #region Receive()
                    foreach (var item in Receive())
                    {
                        yield return item;
                    }
                    #endregion
                }
            }
        }

        private IEnumerable<Command> Compute()
        {
            int src = buffer.Dequeue();
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
            yield return env.SleepUntil(now);
        }

        private IEnumerable<Command> Receive()
        {
            var rcv = env.Receive(spikesIn, waitBefore: inputTime);
            yield return rcv;
            var spike = (int)rcv.Message;
            buffer.Enqueue(spike);
        }
    }
}