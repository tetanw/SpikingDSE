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

            sim.Compile();
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
            reporter = new TraceReporter("res/odin/result.trace");
            var input = sim.AddActor(new SpikeSourceTrace("res/odin/validation.trace", startTime: 4521, reporter: reporter));
            var output = sim.AddActor(new SpikeSink(reporter: reporter));
            var weights = WeigthsUtil.ReadFromCSV("res/odin/weights_256.csv");
            var core1 = sim.AddActor(new ODINCore(256, threshold: 30, weights: weights, synComputeTime: 2, outputTime: 8, inputTime: 7));

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
            var producer = sim.AddActor(new Producer(8, () => "hi", name: "P1"));
            var consumer = sim.AddActor(new Consumer(name: "C1"));

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
            var producer = sim.AddActor(new Producer(4, () => new MeshFlit { DestX = 1, DestY = 1, Message = "hi" }, name: "producer", reporter: reporter));
            var consumer = sim.AddActor(new Consumer(name: "consumer", reporter: reporter));
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
            var producer = sim.AddActor(new Producer(8, () => "hi"));
            var consumer = sim.AddActor(new Consumer());
            var fork = sim.AddActor(new Fork());
            var join = sim.AddActor(new Join());

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

            var producer = sim.AddActor(new Producer(4, () => "hi", reporter: reporter));
            var consumer = sim.AddActor(new Consumer(reporter: reporter));

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
            var producer = sim.AddActor(new Producer(0, () => "Hi", name: "producer", reporter: reporter));
            var buffer = sim.AddActor(new Buffer(5));
            var consumer = sim.AddActor(new Consumer(interval: 3, name: "consumer", reporter: reporter));

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
            var producer = sim.AddActor(new Producer(1, () => "Hi", name: "producer"));
            var fifo = sim.AddActor(new Buffer(1));
            var consumer = sim.AddActor(new Consumer(interval: 3, name: "consumer"));

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
        private int[,] SeparateWeights(int[,] weights, int start, int end)
        {
            int[,] res = new int[256, 256];
            for (int y = start; y < end; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    res[y, x] = weights[x, y];
                }
            }

            return res;
        }

        private Func<object, int> CreatePacketToSpike()
        {
            return (object message) =>
            {
                var flit = (MeshFlit)message;
                return (int)flit.Message;
            };
        }

        private Func<int, object> CreateSpikeToPacket(NeuronLocator<MeshCoord> locator, int srcX, int srcY, int baseID)
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

        private ODINCore createCore(LayerMeshLocator locator, string name, int[,] weights, int baseID, int x, int y)
        {
            return new ODINCore(256,
                name: name,
                threshold: 30,
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
            var reporter = new TraceReporter("res/multi-odin/result.trace");
            // TODO: Mapper does not yet seem correct
            var locator = new LayerMeshLocator();
            locator.AddMapping(new Layer(0, 32), new MeshCoord(0, 0));
            locator.AddMapping(new Layer(32, 64), new MeshCoord(1, 0));
            locator.AddMapping(new Layer(64, 96), new MeshCoord(2, 0));
            locator.AddMapping(new Layer(96, 128), new MeshCoord(3, 0));
            locator.AddMapping(new Layer(256, 1280), new MeshCoord(-1, 0));

            // Create cores
            var weights = WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv");

            // Create mesh
            var routers = MeshUtils.CreateMesh(sim, 4, 1, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));
            var source = sim.AddActor(new SpikeSourceTrace("res/multi-odin/validation.trace", startTime: 4521, reporter: reporter, transformOut: CreateSpikeToPacket(locator, -1, 0, 0)));
            var sink = sim.AddActor(new SpikeSink(reporter: reporter, inTransformer: CreatePacketToSpike()));
            sim.AddChannel(ref source.spikesOut, ref routers[0, 0].inWest);
            sim.AddChannel(ref sink.spikesIn, ref routers[0, 0].outWest);

            var weights1 = SeparateWeights(weights, 0, 32);
            var core1 = sim.AddActor(createCore(locator, "odin1", weights1, 256, 0, 0));
            sim.AddChannel(ref core1.spikesOut, ref routers[0, 0].inLocal);
            sim.AddChannel(ref routers[0, 0].outLocal, ref core1.spikesIn);

            var weights2 = SeparateWeights(weights, 32, 64);
            var core2 = sim.AddActor(createCore(locator, "odin2", weights2, 512, 0, 0));
            sim.AddChannel(ref core2.spikesOut, ref routers[1, 0].inLocal);
            sim.AddChannel(ref routers[1, 0].outLocal, ref core2.spikesIn);

            var weights3 = SeparateWeights(weights, 64, 96);
            var core3 = sim.AddActor(createCore(locator, "odin3", weights3, 768, 0, 0));
            sim.AddChannel(ref core3.spikesOut, ref routers[2, 0].inLocal);
            sim.AddChannel(ref routers[2, 0].outLocal, ref core3.spikesIn);

            var weights4 = SeparateWeights(weights, 96, 128);
            var core4 = sim.AddActor(createCore(locator, "odin4", weights4, 1024, 0, 0));
            sim.AddChannel(ref core4.spikesOut, ref routers[3, 0].inLocal);
            sim.AddChannel(ref routers[3, 0].outLocal, ref core4.spikesIn);
        }

        public override void Cleanup()
        {

        }
    }
}