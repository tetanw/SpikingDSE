using System;

namespace SpikingDSE
{
    public class MultiODINTest : Experiment
    {
        private int[,] SeparateWeights(int[,] weights, int start, int end, int N)
        {
            int inputWidth = weights.GetLength(0);

            int[,] res = new int[N, N];
            for (int y = start; y < end; y++)
            {
                for (int x = 0; x < inputWidth; x++)
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

        private ODINCore addCore(LayerMeshLocator locator, MeshRouter[,] routers, string name, int[,] weights, int baseID, int start, int end, int x, int y)
        {
            var myWeights = SeparateWeights(weights, start, end, 256);
            var core = new ODINCore(256,
                name: name,
                threshold: 30,
                weights: myWeights,
                synComputeTime: 2,
                outputTime: 8,
                inputTime: 7,
                transformOut: CreateSpikeToPacket(locator, x, y, baseID),
                transformIn: CreatePacketToSpike()
            );
            sim.AddActor(core);
            sim.AddChannel(ref core.spikesOut, ref routers[x, y].inLocal);
            sim.AddChannel(ref routers[x, y].outLocal, ref core.spikesIn);

            locator.AddMapping(new Layer(start, end), new MeshCoord(x, y));
            return core;
        }

        public override void Setup()
        {
            var reporter = new TraceReporter("res/multi-odin/result.trace");
            var locator = new LayerMeshLocator();

            // Create cores
            var weights = WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv");

            // Create mesh
            var routers = MeshUtils.CreateMesh(sim, 3, 2, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));

            // Create source-sink
            var source = sim.AddActor(new SpikeSourceTrace("res/multi-odin/validation.trace", startTime: 4521, reporter: reporter, transformOut: CreateSpikeToPacket(locator, 0, 0, 0)));
            var sink = sim.AddActor(new SpikeSink(reporter: reporter, inTransformer: CreatePacketToSpike()));
            sim.AddChannel(ref source.spikesOut, ref routers[0, 1].inLocal);
            sim.AddChannel(ref routers[0, 0].outLocal, ref sink.spikesIn);
            locator.AddMapping(new Layer(256, 1280), new MeshCoord(0, 0));

            // add cores
            var core1 = addCore(locator, routers, "odin1", weights, 256, 0, 32, 1, 0);
            var core2 = addCore(locator, routers, "odin2", weights, 256, 32, 64, 2, 0);
            var core3 = addCore(locator, routers, "odin3", weights, 256, 64, 96, 1, 1);
            var core4 = addCore(locator, routers, "odin4", weights, 256, 96, 128, 2, 1);
        }

        public override void Cleanup()
        {

        }
    }
}