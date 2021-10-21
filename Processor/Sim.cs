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

            var producer = scheduler.AddProcess(new Producer(8, "hi", transformer: (m) => new Packet { ID = 1, Message = m }));
            var consumer = scheduler.AddProcess(new Consumer());
            var ni1 = scheduler.AddProcess(new MeshNI(0, 0));
            var ni2 = scheduler.AddProcess(new MeshNI(1, 0));
            var router1 = new XYRouter(1);
            var router2 = new XYRouter(1);

            scheduler.AddChannel(ref producer.Out, ref ni1.PEIn);
            scheduler.AddChannel(ref ni2.PEOut, ref consumer.In);
            scheduler.AddChannel(ref ni1.MeshOut, ref router1.inPE);
            scheduler.AddChannel(ref router2.outPE, ref ni2.PEIn);
            scheduler.AddChannel(ref router1.outEast, ref router2.inWest);

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
}