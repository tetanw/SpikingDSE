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
            var (idled, time, nrCommands) = sim.RunUntil(simStop.StopTime, simStop.StopCommands);
            stopwatch.Stop();

            Cleanup();

            if (idled)
            {
                Console.WriteLine("Simulation stopped because no more actors could progress");
                sim.PrintDeadlockReport();
            }
            else
            {
                Console.WriteLine("Simulation stopped because one of the stopping conditions was reached");
            }
            Console.WriteLine($"Simulation was stopped at time: {time:n}");
            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }

        public abstract void Setup();
        public abstract void Cleanup();
    }

    public class WeigthsUtil
    {
        public static double[,] ReadFromCSV(string path)
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

            CorrectWeights(weights);
            return weights;
        }
        private static void Swap(int c, int x, int y, double[,] array)
        {
            // swap index x and y
            var buffer = array[c, x];
            array[c, x] = array[c, y];
            array[c, y] = buffer;
        }

        private static void CorrectWeights(double[,] weights)
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

        public static void ToCSV(string path, double[,] weights)
        {
            StreamWriter sw = new StreamWriter(path);
            int width = weights.GetLength(0);
            int height = weights.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                string[] parts = new string[width];
                for (int x = 0; x < width; x++)
                {
                    parts[x] = weights[x, y].ToString();
                }
                sw.WriteLine(string.Join(",", parts));
            }


            sw.Flush();
            sw.Close();
        }
    }

    public class ODINSingleCore : Experiment
    {
        private TraceReporter reporter;

        public override void Setup()
        {
            reporter = new TraceReporter("res/exp1/result.trace");
            var input = sim.AddProcess(new SpikeSourceTrace("res/exp1/validation.trace", startTime: 4521, reporter: reporter));
            var output = sim.AddProcess(new SpikeSink(reporter: reporter));
            var weights = WeigthsUtil.ReadFromCSV("res/exp1/weights_256.csv");
            var core1 = sim.AddProcess(new ODINCore(256, threshold: 30.0, weights: weights, synComputeTime: 2, outputTime: 8, inputTime: 7));

            sim.AddChannel(ref core1.spikesIn, ref input.spikesOut);
            sim.AddChannel(ref output.spikesIn, ref core1.spikesOut);
        }

        public override void Cleanup()
        {
            reporter.Cleanup();
        }
    }

    public class ProducerConsumer : Experiment
    {
        public override void Setup()
        {
            var producer = sim.AddProcess(new Producer(8, () => "hi", name: "P1"));
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
        class Reporter : ConsumerReporter, ProducerReport
        {
            public void Consumed(Consumer consumer, long time, object message)
            {
                var realMessage = ((MeshFlit)message).Message;
                Console.WriteLine($"[{time}] {consumer.Name} received message {realMessage}");
            }

            public void Produced(Producer producer, long time, object message)
            {
                var realMessage = ((MeshFlit)message).Message;
                Console.WriteLine($"[{time}] {producer.Name} sent message {realMessage}");
            }
        }

        public override void Setup()
        {
            var reporter = new Reporter();
            var producer = sim.AddProcess(new Producer(4, () => new MeshFlit { DestX = 1, DestY = 1, Message = "hi" }, name: "producer", reporter: reporter));
            var consumer = sim.AddProcess(new Consumer(name: "consumer", reporter: reporter));
            var routers = MeshUtils.CreateMesh(sim, 2, 2, (x, y) => new XYRouter(x, y, 1, name: $"router({x},{y})"));

            sim.AddChannel(ref routers[0, 0].fromCore, ref producer.Out);
            sim.AddChannel(ref routers[1, 1].toCore, ref consumer.In);

            simStop.StopTime = 10;
        }

        public override void Cleanup()
        {

        }
    }

    public class ForkJoin : Experiment
    {
        public override void Setup()
        {
            var producer = sim.AddProcess(new Producer(8, () => "hi"));
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

    public class ReportingTest : Experiment
    {
        public class Reporter : ProducerReport, ConsumerReporter
        {
            public void Consumed(Consumer consumer, long time, object message)
            {
                Console.WriteLine($"[{time}][Consumer] Received message: {message}");
            }

            public void Produced(Producer producer, long time, object message)
            {
                Console.WriteLine($"[{time}][Producer] Produced message: {message}");
            }
        }

        public override void Setup()
        {
            var reporter = new Reporter();

            var producer = sim.AddProcess(new Producer(4, () => "hi", reporter: reporter));
            var consumer = sim.AddProcess(new Consumer(reporter: reporter));

            sim.AddChannel(ref producer.Out, ref consumer.In);
            simStop.StopTime = 10;
        }

        public override void Cleanup()
        {

        }
    }

    public class TraceReporter : SpikeSourceTraceReporter, SpikeSinkReporter
    {
        private StreamWriter sw;

        public TraceReporter(string reportPath)
        {
            this.sw = new StreamWriter(reportPath);
        }

        public void SpikeReceived(SpikeSink sink, int neuron, long time)
        {
            sw.WriteLine($"1,{neuron},{time}");
        }

        public void SpikeSent(SpikeSourceTrace source, int neuron, long time)
        {
            sw.WriteLine($"0,{neuron},{time}");
        }

        public void Cleanup()
        {
            sw.Flush();
            sw.Close();
        }
    }

    public interface NeuronLocator<T>
    {
        public T Locate(int neuron);
    }

    public class MeshLocator : NeuronLocator<(int x, int y)>
    {
        public (int x, int y) Locate(int destNeuron)
        {
            if (destNeuron < 128)
            {
                return (0, 0);
            }
            else if (destNeuron < 256)
            {
                return (1, 0);
            }
            else if (destNeuron < 768)
            {
                return (-1, 0);
            }
            else
            {
                throw new Exception($"Can not map neuron {destNeuron}");
            }
        }
    }

    public class MultiODIN : Experiment
    {
        private TraceReporter reporter;

        private (double[,] a, double[,] b) SeperateWeights(double[,] weights)
        {
            double[,] a = new double[weights.GetLength(0), weights.GetLength(1)];
            double[,] b = new double[weights.GetLength(0), weights.GetLength(1)];

            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    a[y, x] = weights[x, y];
                }
            }

            for (int y = 128; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    b[y, x] = weights[x, y];
                }
            }

            return (a, b);
        }

        private Func<object, int> CreatePacketToSpike()
        {
            return (object message) =>
            {
                var flit = (MeshFlit)message;
                return (int)flit.Message;
            };
        }

        private Func<int, object> CreateSpikeToPacket(NeuronLocator<(int x, int y)> locator, int srcX, int srcY, int baseID)
        {
            return (neuron) =>
            {
                var newNeuron = baseID + neuron;
                var (destX, destY) = locator.Locate(newNeuron);
                return new MeshFlit
                {
                    SrcX = srcX,
                    SrcY = srcY,
                    DestX = destX,
                    DestY = destY,
                    Message = newNeuron
                };
            };
        }

        private ODINCore createCore(MeshLocator locator, string name, double[,] weights, int baseID, int x, int y)
        {
            return new ODINCore(256,
                name: name,
                threshold: 30.0,
                weights: weights,
                synComputeTime: 2,
                outputTime: 8,
                inputTime: 7,
                transformOut: CreateSpikeToPacket(locator, x, y, baseID),
                transformIn: CreatePacketToSpike()
            );
        }

        public override void Setup()
        {
            reporter = new TraceReporter("res/exp1/result2.trace");
            // TODO: Let mesh locator do something useful
            var locator = new MeshLocator();

            // Create cores
            var weights = WeigthsUtil.ReadFromCSV("res/exp1/weights_256.csv");
            var (weights1, weights2) = SeperateWeights(weights);
            WeigthsUtil.ToCSV("res/weights1.csv", weights1);
            WeigthsUtil.ToCSV("res/weights2.csv", weights2);

            // Create mesh
            var routers = MeshUtils.CreateMesh(sim, 2, 1, (x, y) => new XYRouter(x, y, 1, name: $"router({x},{y})"));
            var source = sim.AddProcess(new SpikeSourceTrace("res/exp1/validation2.trace", startTime: 4521, reporter: reporter, transformOut: CreateSpikeToPacket(locator, -1, 0, 0)));
            var sink = sim.AddProcess(new SpikeSink(reporter: reporter, inTransformer: CreatePacketToSpike()));
            sim.AddChannel(ref source.spikesOut, ref routers[0, 0].inWest);
            sim.AddChannel(ref sink.spikesIn, ref routers[0, 0].outWest);

            var core1 = sim.AddProcess(createCore(locator, "Odin1", weights, 256, 0, 0));
            sim.AddChannel(ref core1.spikesOut, ref routers[0, 0].fromCore);
            sim.AddChannel(ref routers[0, 0].toCore, ref core1.spikesIn);

            var core2 = sim.AddProcess(createCore(locator, "Odin2", weights, 512, 1, 0));
            sim.AddChannel(ref core2.spikesOut, ref routers[1, 0].fromCore);
            sim.AddChannel(ref routers[1, 0].toCore, ref core2.spikesIn);
        }

        public override void Cleanup()
        {
            reporter.Cleanup();
        }
    }
}