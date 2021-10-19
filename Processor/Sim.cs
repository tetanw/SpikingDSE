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
            var router = scheduler.AddProcess(new Router());
            var core1 = scheduler.AddProcess(new Core(1));
            var core2 = scheduler.AddProcess(new Core(2));
            var core3 = scheduler.AddProcess(new Core(3));

            scheduler.AddChannel(ref io.spikesOut, ref router.spikesIn);
            scheduler.AddChannel(ref router.spikesOut1, ref core1.spikesIn);
            scheduler.AddChannel(ref router.spikesOut2, ref core2.spikesIn);
            scheduler.AddChannel(ref router.spikesOut3, ref core3.spikesIn);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(1_000_000);
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

        public override IEnumerator<Command> Run()
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
        private List<int> buffer = new List<int>();
        private int coreID;

        public Core(int coreID)
        {
            this.coreID = coreID;
        }

        public override IEnumerator<Command> Run()
        {
            while (buffer.Count < 10)
            {
                yield return env.Receive(spikesIn);
                var spike = (int)env.Received;
                buffer.Add(spike);
                Console.WriteLine($"[{env.Now}]: core {coreID} received {spike}");
            }
        }
    }

    public class Router : Process
    {
        public Port spikesIn = new Port();
        public Port spikesOut1 = new Port();
        public Port spikesOut2 = new Port();
        public Port spikesOut3 = new Port();

        public override IEnumerator<Command> Run()
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