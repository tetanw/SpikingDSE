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
            var core1 = scheduler.AddProcess(new Core(1, 10));

            scheduler.AddChannel(ref io.spikesOut, ref core1.spikesIn);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(int.MaxValue, 10_000_000);
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
            int spike = 1;
            while (true)
            {
                yield return env.Delay(1);
                yield return env.Send(spikesOut, spike++);
            }
        }
    }

    public class Core : Process
    {
        public Port spikesIn = new Port();
        public Port spikesOut = new Port();
        private Queue<int> buffer = new Queue<int>();
        private int bufferCap;
        private int coreID;

        public Core(int coreID, int bufferCap)
        {
            this.coreID = coreID;
            this.bufferCap = bufferCap;
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
            buffer.Dequeue();
            yield return env.Delay(16);
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