using System.Collections.Generic;
using System;
using System.Diagnostics;

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

            var io = scheduler.AddProcess(new IO(1));
            var core1 = scheduler.AddProcess(new ODINCore(1, 10, 256));

            scheduler.AddChannel(ref io.spikesOut, ref core1.spikesIn);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(int.MaxValue, 1_000_000);
            stopwatch.Stop();
            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }
    }

    public class IO : Process
    {
        public Port spikesOut = new Port();
        private int interval;

        public IO(int interval)
        {
            this.interval = interval;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                yield return env.Delay(1);
                yield return env.Send(spikesOut, 1);
            }
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

        public ODINCore(int coreID, int bufferCap, int nrNeurons, double threshold = 0.1, int synComputeTime = 0)
        {
            this.coreID = coreID;
            this.bufferCap = bufferCap;
            this.weights = new double[nrNeurons, nrNeurons];
            this.pots = new double[nrNeurons];
            this.nrNeurons = nrNeurons;
            this.threshold = threshold;
            this.synComputeTime = synComputeTime;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                if (buffer.Count == bufferCap)
                {
                    foreach (var item in Compute())
                    {
                        yield return item;
                    }
                }
                if (buffer.Count != bufferCap && env.Ready(spikesIn))
                {
                    yield return env.Receive(spikesIn);
                    var spike = (int)env.Received;
                    buffer.Enqueue(spike);
                }
            }
        }

        private IEnumerable<Command> Compute()
        {
            int neuron = buffer.Dequeue();
            yield return env.Delay(16);

            int src = neuron;
            int start = env.Now;
            for (int dst = 0; dst < nrNeurons; dst++)
            {
                pots[dst] = weights[src, dst] * 1.0;
                if (pots[dst] > threshold)
                {
                    yield return env.SendAt(spikesOut, 1, start + dst * synComputeTime);
                }
            }
        }
    }

    public class Router : Process
    {
        public Port spikesIn = new Port();
        public Port spikesOut1 = new Port();
        public Port spikesOut2 = new Port();
        public Port spikesOut3 = new Port();

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                yield return env.Receive(spikesIn);
                var spike = (int)env.Received;

                yield return env.Send(spikesOut1, spike);
                yield return env.Send(spikesOut2, spike);
                yield return env.Send(spikesOut3, spike);
            }
        }
    }

}