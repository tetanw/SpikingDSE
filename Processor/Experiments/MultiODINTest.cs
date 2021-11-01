using System;

namespace SpikingDSE
{
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