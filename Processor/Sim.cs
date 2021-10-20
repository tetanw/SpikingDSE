using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;

namespace SpikingDSE
{
    public class Simulation
    {
        public Simulation()
        {

        }

        private void Swap(int c, int x, int y, double[,] array)
        {
            // swap index x and y
            var buffer = array[c, x];
            array[c, x] = array[c, y];
            array[c, y] = buffer;
        }

        private void CorrectWeights(double[,] weights)
        {
            for (int x = 0; x < 256; x++)
            {
                for (int y = 0; y < 256; y += 8)
                {
                    Swap(x, y + 0, y + 7, weights);
                    Swap(x, y + 1, y + 6, weights);
                    Swap(x, y + 2, y + 5, weights);
                    Swap(x, y + 3, y + 4, weights);
                }
            }
        }

        public void Run()
        {
            var scheduler = new Scheduler();

            var report = new StreamWriter("res/exp1/result.trace");
            var input = scheduler.AddProcess(new EventTraceIn("res/exp1/validation.trace", report, startTime: 4521));
            var output = scheduler.AddProcess(new SpikeSink(report));
            var weights = Weights.ReadFromCSV("res/exp1/weights_256.csv");
            CorrectWeights(weights);
            var core1 = scheduler.AddProcess(new ODINCore(1, 256, bufferCap: 1, threshold: 30.0, weights: weights, synComputeTime: 2, outputTime: 8, inputTime: 7));

            scheduler.AddChannel(ref core1.spikesIn, ref input.spikesOut);
            scheduler.AddChannel(ref output.spikesIn, ref core1.spikesOut);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(int.MaxValue, int.MaxValue);
            stopwatch.Stop();
            report.Flush();
            report.Close();

            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }
    }

    public class EventTraceIn : Process
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

    public class SpikeSink : Process
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
                yield return env.Receive(spikesIn);
                int spike = (int)spikesIn.Message;
                sw.WriteLine($"1,{spike},{env.Now}");
            }
        }
    }

    public class ODINCore : Process
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
            yield return env.Receive(spikesIn, duration: inputTime);
            var spike = (int)spikesIn.Message;
            buffer.Enqueue(spike);
        }
    }

}