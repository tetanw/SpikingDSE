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
        public long StopEvents = long.MaxValue;
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
            Console.WriteLine("Simulation starting");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var (time, nrEvents) = sim.RunUntil(simStop.StopTime, simStop.StopEvents);
            stopwatch.Stop();

            Cleanup();

            Console.WriteLine("Simulation done");
            sim.PrintDeadlockReport();
            Console.WriteLine($"Simulation was stopped at time: {time:n}");
            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Events handled: {nrEvents:n}");
            Console.WriteLine($"Performance was about: {nrEvents / stopwatch.Elapsed.TotalSeconds:n} event/s");
            Console.WriteLine($"Time per event: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrEvents, "s")}");
        }

        public abstract void Setup();
        public abstract void Cleanup();
    }

    public class WeigthsUtil
    {
        public static int[,] ReadFromCSV(string path)
        {
            int[,] weights = null;
            int currentLine = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                int[] numbers = line.Split(",").Select(t => int.Parse(t)).ToArray();
                if (weights == null)
                {
                    weights = new int[numbers.Length, numbers.Length];
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
        private static void Swap(int c, int x, int y, int[,] array)
        {
            // swap index x and y
            var buffer = array[c, x];
            array[c, x] = array[c, y];
            array[c, y] = buffer;
        }

        private static void CorrectWeights(int[,] weights)
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

        public static void ToCSV(string path, int[,] weights)
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
            var core1 = sim.AddProcess(new ODINCore(256, threshold: 30, weights: weights, synComputeTime: 2, outputTime: 8, inputTime: 7));

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

            simStop.StopEvents = 10_000_000;
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

            sim.AddChannel(ref routers[0, 0].inLocal, ref producer.Out);
            sim.AddChannel(ref routers[1, 1].outLocal, ref consumer.In);

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

            simStop.StopEvents = 100;
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

    public class ResTest : Experiment
    {
        class Reporter : ProducerReport, ConsumerReporter
        {
            public void Consumed(Consumer consumer, long time, object message)
            {
                Console.WriteLine($"[{time}] consumer received {message}");
            }

            public void Produced(Producer producer, long time, object message)
            {
                Console.WriteLine($"[{time}] producer sent {message}");
            }
        }

        public override void Setup()
        {
            var reporter = new Reporter();
            var producer = sim.AddProcess(new Producer(0, () => "Hi", name: "producer", reporter: reporter));
            var buffer = sim.AddProcess(new Buffer(5));
            var consumer = sim.AddProcess(new Consumer(interval: 3, name: "consumer", reporter: reporter));

            sim.AddChannel(ref producer.Out, ref buffer.input);
            sim.AddChannel(ref buffer.output, ref consumer.In);

            simStop.StopTime = 100;
        }

        public override void Cleanup()
        {
            
        }

    }

     public class ResPerf : Experiment
    {
        public override void Setup()
        {
            var producer = sim.AddProcess(new Producer(1, () => "Hi", name: "producer"));
            var fifo = sim.AddProcess(new Buffer(1));
            var consumer = sim.AddProcess(new Consumer(interval: 3, name: "consumer"));

            sim.AddChannel(ref producer.Out, ref fifo.input);
            sim.AddChannel(ref fifo.output, ref consumer.In);

            simStop.StopEvents = 10_000_000;
        }

        public override void Cleanup()
        {

        }

    }

    public class MultiODINTest : Experiment
    {
        class Reporter : ODINReporter, SpikeSinkReporter, SpikeSourceTraceReporter
        {
            public void ProducedSpike(ODINCore core, long time, int neuron)
            {
                // Console.WriteLine($"[{time}] Core produced output spike {neuron}");
            }

            public void ReceivedSpike(ODINCore core, long time, int neuron)
            {
                // Console.WriteLine($"[{time}] Core received output spike {neuron}");
            }

            public void SpikeReceived(SpikeSink sink, int neuron, long time)
            {
                // Console.WriteLine($"[{time}] Sink received neuron {neuron}");
            }

            public void SpikeSent(SpikeSourceTrace source, int neuron, long time)
            {
                // Console.WriteLine($"[{time}] Source sent neuron {neuron}");
            }
        }

        private (int[,] a, int[,] b) SeperateWeights(int[,] weights)
        {
            int[,] a = new int[weights.GetLength(0), weights.GetLength(1)];
            int[,] b = new int[weights.GetLength(0), weights.GetLength(1)];

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

        private ODINCore createCore(MeshLocator locator, string name, int[,] weights, int baseID, int x, int y, Reporter reporter)
        {
            return new ODINCore(256,
                name: name,
                threshold: 30,
                weights: weights,
                synComputeTime: 2,
                outputTime: 8,
                inputTime: 7,
                transformOut: CreateSpikeToPacket(locator, x, y, baseID),
                transformIn: CreatePacketToSpike(),
                reporter: reporter
            );
        }

        public override void Setup()
        {
            var reporter = new Reporter();
            // TODO: Let mesh locator do something useful
            var locator = new MeshLocator();

            // Create cores
            var weights = WeigthsUtil.ReadFromCSV("res/exp1/weights_256.csv");
            var (weights1, weights2) = SeperateWeights(weights);
            WeigthsUtil.ToCSV("res/weights1.csv", weights1);
            WeigthsUtil.ToCSV("res/weights2.csv", weights2);

            // Create mesh
            var routers = MeshUtils.CreateMesh(sim, 1, 1, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));
            var source = sim.AddProcess(new SpikeSourceTrace("res/exp1/validation.trace", startTime: 4521, reporter: reporter, transformOut: CreateSpikeToPacket(locator, -1, 0, 0)));
            var sink = sim.AddProcess(new SpikeSink(reporter: reporter, inTransformer: CreatePacketToSpike()));
            sim.AddChannel(ref source.spikesOut, ref routers[0, 0].inWest);
            sim.AddChannel(ref sink.spikesIn, ref routers[0, 0].outWest);

            var core1 = sim.AddProcess(createCore(locator, "Odin1", weights, 256, 0, 0, reporter));
            sim.AddChannel(ref core1.spikesOut, ref routers[0, 0].inLocal);
            sim.AddChannel(ref routers[0, 0].outLocal, ref core1.spikesIn);
        }

        public override void Cleanup()
        {
            
        }
    }
}