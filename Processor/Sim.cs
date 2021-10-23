using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE
{
    public class SimStopConditions
    {
        public long StopTime = long.MaxValue;
        public long StopCommands = long.MaxValue;
    }

    public abstract class Experiment
    {
        protected Simulator sim;
        protected SimStopConditions simStop;

        public Experiment()
        {
            sim = new Simulator();
            simStop = new SimStopConditions();
        }

        public void Run()
        {
            Setup();

            sim.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var (time, nrCommands) = sim.RunUntil(simStop.StopTime, simStop.StopCommands);
            stopwatch.Stop();

            Cleanup();

            Console.WriteLine($"Simulation was stopped at time: {time}");
            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }

        public abstract void Setup();
        public abstract void Cleanup();
    }

    public class ODINSingleCore : Experiment
    {
        private StreamWriter report;

        private double[,] ReadFromCSV(string path)
        {
            double[,] weights = null;
            int currentLine = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                double[] numbers = line.Split(",").Select(t => double.Parse(t)).ToArray();
                if (weights == null)
                {
                    weights = new double[numbers.Length, numbers.Length];
                }

                for (int i = 0; i < numbers.Length; i++)
                {
                    weights[i, currentLine] = numbers[i];
                }
                currentLine++;
            }

            return weights;
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

        public override void Setup()
        {
            report = new StreamWriter("res/exp1/result.trace");
            var input = sim.AddProcess(new EventTraceIn("res/exp1/validation.trace", report, startTime: 4521));
            var output = sim.AddProcess(new SpikeSink(report));
            var weights = ReadFromCSV("res/exp1/weights_256.csv");
            CorrectWeights(weights);
            var core1 = sim.AddProcess(new ODINCore(1, 256, bufferCap: 1, threshold: 30.0, weights: weights, synComputeTime: 2, outputTime: 8, inputTime: 7));

            sim.AddChannel(ref core1.spikesIn, ref input.spikesOut);
            sim.AddChannel(ref output.spikesIn, ref core1.spikesOut);
        }

        public override void Cleanup()
        {
            report.Flush();
            report.Close();
        }
    }

    public class ProducerConsumer : Experiment
    {
        public override void Setup()
        {
            var producer = sim.AddProcess(new Producer(8, "hi", name: "P1"));
            var consumer = sim.AddProcess(new Consumer(name: "C1"));

            sim.AddChannel(ref consumer.In, ref producer.Out);

            simStop.StopCommands = 10_000_000;
        }

        public override void Cleanup()
        {

        }

    }

    public class MeshTest : Experiment
    {

        public override void Setup()
        {
            var producer = sim.AddProcess(new Producer(8, new Packet { ID = 3, Message = "hi" }, name: "producer"));
            var consumer = sim.AddProcess(new Consumer(name: "consumer"));
            var locator = new MeshLocator(2, 2);
            var ni1 = sim.AddProcess(new MeshNI(0, 0, locator, name: "ni1"));
            var ni2 = sim.AddProcess(new MeshNI(1, 1, locator, name: "ni2"));
            var routers = new XYRouter[2, 2];
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    routers[x, y] = sim.AddProcess(new XYRouter(1, name: $"router {x},{y}"));
                }
            }

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    // wire up west side if possible
                    if (x > 0)
                    {
                        sim.AddChannel(ref routers[x, y].outWest, ref routers[x - 1, y].inEast);
                    }

                    // wire up east side if possible
                    if (x < 1)
                    {
                        sim.AddChannel(ref routers[x, y].outEast, ref routers[x + 1, y].inWest);
                    }

                    // wire up south side if possible
                    if (y > 0)
                    {
                        sim.AddChannel(ref routers[x, y].outSouth, ref routers[x, y - 1].inNorth);
                    }

                    // wire up north side if possible
                    if (y < 1)
                    {
                        sim.AddChannel(ref routers[x, y].outNorth, ref routers[x, y + 1].inSouth);
                    }
                }
            }
            sim.AddChannel(ref routers[1, 1].toCore, ref ni2.FromMesh);
            sim.AddChannel(ref routers[0, 0].fromCore, ref ni1.ToMesh);

            sim.AddChannel(ref producer.Out, ref ni1.FromCore);
            sim.AddChannel(ref ni2.ToCore, ref consumer.In);

            simStop.StopCommands = 10_000;
        }

        public override void Cleanup()
        {

        }
    }

    public class ForkJoin : Experiment
    {
        public override void Setup()
        {
            var producer = sim.AddProcess(new Producer(8, "hi"));
            var consumer = sim.AddProcess(new Consumer());
            var fork = sim.AddProcess(new Fork());
            var join = sim.AddProcess(new Join());

            sim.AddChannel(ref producer.Out, ref fork.input);
            sim.AddChannel(ref fork.out1, ref join.in1);
            sim.AddChannel(ref fork.out2, ref join.in2);
            sim.AddChannel(ref fork.out3, ref join.in3);
            sim.AddChannel(ref join.output, ref consumer.In);

            simStop.StopCommands = 100;
        }

        public override void Cleanup()
        {

        }
    }
}