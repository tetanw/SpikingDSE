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

            var input = scheduler.AddProcess(new EventTraceIn("res/events_test.trace"));
            var output = scheduler.AddProcess(new SpikeSink("res/out.trace"));
            var weights = Weights.ReadFromCSV("res/weights.csv");
            var core1 = scheduler.AddProcess(new ODINCore(1, 10, 256, threshold: 32.0, weights: weights));

            scheduler.AddChannel(ref input.spikesOut, ref core1.spikesIn);
            scheduler.AddChannel(ref core1.spikesOut, ref output.spikesIn);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(int.MaxValue, int.MaxValue);
            stopwatch.Stop();
            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }
    }

    public class EventTraceIn : Process
    {
        public Port spikesOut = new Port();
        private string path;

        public EventTraceIn(string path)
        {
            this.path = path;
        }

        public override IEnumerable<Command> Run()
        {
            foreach (var (neuron, time) in EventTraceReader.ReadInputs(path, 100_000_000))
            {
                yield return env.SleepUntil(time);
                yield return env.Send(spikesOut, neuron);
            }
        }
    }

    public class SpikeSink : Process
    {
        public Port spikesIn = new Port();

        private string path;
        private StreamWriter sw;

        public SpikeSink(string path)
        {
            this.path = path;
            this.sw = new StreamWriter(path);
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                yield return env.Receive(spikesIn);
                int spike = (int)env.Received;
                sw.WriteLine($"1,{spike},{env.Now}");
            }
        }

        public override void Finish()
        {
            sw.Flush();
            sw.Close();
        }
    }

    public class ODINCore : Process
    {
        public Port spikesIn = new Port();
        public Port spikesOut = new Port();
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
                if (buffer.Count != bufferCap && env.Ready(spikesIn))
                {
                    yield return env.Receive(spikesIn);
                    var spike = (int)env.Received;
                    yield return env.Delay(inputTime);
                    buffer.Enqueue(spike);
                }
            }
        }

        private IEnumerable<Command> Compute()
        {
            int src = buffer.Dequeue();
            long now = env.Now;
            for (int dst = 0; dst < nrNeurons; dst++)
            {
                pots[dst] += weights[dst, src];
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
    }

}