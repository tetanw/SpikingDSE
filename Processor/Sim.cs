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

        public void Run()
        {
            var scheduler = new Scheduler();

            var report = new StreamWriter("res/exp1/result.trace");
            var input = scheduler.AddProcess(new EventTraceIn("res/exp1/validation.trace", report));
            var output = scheduler.AddProcess(new SpikeSink(report));
            var weights = Weights.ReadFromCSV("res/exp1/weights_256.csv");
            var core1 = scheduler.AddProcess(new ODINCore(1, 10, 256, threshold: 30.0, weights: weights, synComputeTime: 2));

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

        public EventTraceIn(string path, StreamWriter sw)
        {
            this.path = path;
            this.sw = sw;
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
            foreach (var (neuron, time) in EventTraceReader.ReadInputs(path, 100_000_000))
            {
                yield return env.SleepUntil(time);
                yield return env.Send(spikesOut, neuron);
                sw.WriteLine($"0,{neuron},{time}");
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

        public ODINCore(int coreID, int bufferCap, int nrNeurons, double[,] weights = null, double threshold = 0.1, int synComputeTime = 0, int outputTime = 0, int inputTime = 0)
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
                if (buffer.Count != bufferCap && spikesIn.Ready)
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
            long now = env.Now;
            for (int dst = 0; dst < nrNeurons; dst++)
            {
                pots[dst] += weights[src, dst];
                now += synComputeTime;
                if (pots[dst] > threshold)
                {
                    now += outputTime;
                    pots[dst] = 0.0;
                    yield return env.SendAt(spikesOut, dst, now);
                }
            }
            yield return env.SleepUntil(now);
        }

        private IEnumerable<Command> Receive()
        {
            yield return env.Receive(spikesIn);
            var spike = (int)spikesIn.Message;
            yield return env.Delay(inputTime);
            buffer.Enqueue(spike);
        }
    }

}