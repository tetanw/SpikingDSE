using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;

namespace SpikingDSE
{
    public class ODINSingleCore
    {
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

    public class ProducerConsumer
    {
        public void Run()
        {
            var scheduler = new Scheduler();

            var producer = scheduler.AddProcess(new Producer(8, "hi"));
            var consumer = scheduler.AddProcess(new Consumer());

            scheduler.AddChannel(ref consumer.In, ref producer.Out);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(int.MaxValue, stopCmds: 10_000_000);
            stopwatch.Stop();

            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }
    }

    public class MultiCore
    {

        public void Run()
        {
            var scheduler = new Scheduler();

            var router1 = new XYRouter(1);
            var router2 = new XYRouter(1);
            var core2 = new DummyCore(DummyCoreMode.Source, interval: 1, destX: 1, destY: 1);
            var core1 = new DummyCore(DummyCoreMode.Sink);


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

    public enum DummyCoreMode
    {
        Source,
        Sink
    }

    public class DummyCore : Process
    {
        public InPort coreIn;
        public OutPort coreOut;

        private DummyCoreMode mode;
        private int interval;
        private int destX, destY;

        public DummyCore(DummyCoreMode mode, int interval = 0, int destX = 0, int destY = 0)
        {
            this.mode = mode;
            this.interval = interval;
            this.destX = destX;
            this.destY = destY;
        }

        public override IEnumerable<Command> Run()
        {
            if (mode == DummyCoreMode.Source)
            {
                while (true)
                {
                    var packet = new XYPacket()
                    {
                        X = destX,
                        Y = destY,
                        Message = "hello"
                    };
                    yield return env.Delay(interval);
                    yield return env.Send(coreOut, packet);
                }
            }
            else if (mode == DummyCoreMode.Sink)
            {
                while (true)
                {
                    yield return env.Receive(coreIn);
                }
            }
        }
    }
}