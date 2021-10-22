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

    public class MeshLocator : Locator<(int x, int y)>
    {
        private int width, height;

        public MeshLocator(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public (int x, int y) Locate(int packetID)
        {
            int x = packetID % width;
            int y = packetID / width;
            return (x, y);
        }
    }

    public class MeshTest
    {

        public void Run()
        {
            var scheduler = new Scheduler();

            var producer = scheduler.AddProcess(new Producer(8, new Packet { ID = 3, Message = "hi" }, name: "producer"));
            var consumer = scheduler.AddProcess(new Consumer(name: "consumer"));
            var locator = new MeshLocator(2, 2);
            var ni1 = scheduler.AddProcess(new MeshNI(0, 0, locator, name: "ni1"));
            var ni2 = scheduler.AddProcess(new MeshNI(1, 1, locator, name: "ni2"));
            var routers = new XYRouter[2, 2];
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    routers[x, y] = scheduler.AddProcess(new XYRouter(1, name: $"router {x},{y}"));
                }
            }

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    // wire up west side if possible
                    if (x > 0)
                    {
                        scheduler.AddChannel(ref routers[x, y].outWest, ref routers[x - 1, y].inEast);
                    }

                    // wire up east side if possible
                    if (x < 1)
                    {
                        scheduler.AddChannel(ref routers[x, y].outEast, ref routers[x + 1, y].inWest);
                    }

                    // wire up south side if possible
                    if (y > 0)
                    {
                        scheduler.AddChannel(ref routers[x, y].outSouth, ref routers[x, y - 1].inNorth);
                    }

                    // wire up north side if possible
                    if (y < 1)
                    {
                        scheduler.AddChannel(ref routers[x, y].outNorth, ref routers[x, y + 1].inSouth);
                    }
                }
            }
            scheduler.AddChannel(ref routers[1, 1].toCore, ref ni2.FromMesh);
            scheduler.AddChannel(ref routers[0, 0].fromCore, ref ni1.ToMesh);

            scheduler.AddChannel(ref producer.Out, ref ni1.FromCore);
            scheduler.AddChannel(ref ni2.ToCore, ref consumer.In);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(1_000_000, int.MaxValue);
            stopwatch.Stop();

            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }
    }

    public class ForkJoin
    {

        public void Run()
        {
            var scheduler = new Scheduler();

            var producer = scheduler.AddProcess(new Producer(8, "hi"));
            var consumer = scheduler.AddProcess(new Consumer());
            var fork = scheduler.AddProcess(new Fork());
            var join = scheduler.AddProcess(new Join());

            scheduler.AddChannel(ref producer.Out, ref fork.input, "Producer -> Fork");
            scheduler.AddChannel(ref fork.out1, ref join.in1, "Fork -> Join 1");
            scheduler.AddChannel(ref fork.out2, ref join.in2, "Fork -> Join 2");
            scheduler.AddChannel(ref fork.out3, ref join.in3, "Fork -> Join 3");
            scheduler.AddChannel(ref join.output, ref consumer.In, "Join -> Consumer");

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
}