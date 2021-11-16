using System;

namespace SpikingDSE
{
    public class MultiODINTest : Experiment
    {
        private SNN snn;
        private MeshRouter[,] routers;
        private TraceReporter trace;
        private TensorReporter tensor;
        private MemReporter mem;

        private ODINController AddController(SNN snn, int x, int y)
        {
            var controllerCoord = new MeshCoord(x, y);
            var controller = sim.AddActor(new ODINController(controllerCoord, snn, 0, 100_000, name: "controller"));
            // controller.TimeAdvanced += (_, ts) => trace.AdvanceTimestep(ts);
            controller.TimeAdvanced += (_, ts) =>
            {
                tensor.AdvanceTimestep(ts);
                Console.WriteLine($"Advanced to timestep: {ts}");
            };
            controller.SpikeReceived += (_, _, spike) =>
            {
                tensor.InformSpike(spike.layer, spike.neuron);
            };
            sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
            sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
            return controller;
        }

        private (ODINCore, ODINNI) AddCore(ODINController controller, ODINDelayModel delayModel, int size, int x, int y, string name)
        {
            var coreCoord = new MeshCoord(x, y);
            var core = sim.AddActor(new ODINCore(coreCoord, size, delayModel, enableRefractory: true, name: name));
            core.ReceivedTimeref += (_, _, ts, layer) =>
            {
                mem.AdvanceLayer(layer, ts, layer.pots);
            };
            // core.ReceivedSpike += (_, time, ev) => trace.OutputSpike(ev.neuron, time);
            // core.ProducedSpike += (_, time, ev) => trace.InputSpike(ev.neuron, time);
            // core.ReceivedTimeref += (_, time) => trace.TimeRef(time);
            var coreNi = sim.AddActor(new ODINNI(coreCoord, (MeshCoord)controller.GetLocation()));
            sim.AddChannel(coreNi.outRouter, routers[x, y].inLocal);
            sim.AddChannel(routers[x, y].outLocal, coreNi.inRouter);
            sim.AddChannel(coreNi.outLocal, core.input);
            sim.AddChannel(core.output, coreNi.inLocal);
            return (core, coreNi);
        }

        private LIFLayer RLIF2LIF(RLIFLayer rlif)
        {
            float[,] newWeights = new float[rlif.Size + rlif.InputSize, rlif.Size];
            // all feed-forward connections
            for (int src = 0; src < rlif.InputSize; src++)
            {
                for (int dst = 0; dst < rlif.Size; dst++)
                {
                    newWeights[src, dst] = rlif.inWeights[src, dst];
                }
            }
            // then all recurrent connections
            for (int src = rlif.InputSize; src < rlif.InputSize + rlif.Size; src++)
            {
                for (int dst = 0; dst < rlif.Size; dst++)
                {
                    newWeights[src, dst] = rlif.recWeights[src - rlif.InputSize, dst];
                }
            }
            return new LIFLayer(newWeights, name: rlif.Name);
        }

        public override void Setup()
        {
            // Extra assumptions to get ODIN working
            // 1. ODIN has a refractory mode
            // 2. Layer is also transmitted
            // 3. Both excitatory and inhibitory spikes can be used together
            // 4. Leakage is proportional to current voltage instead of a constant????

            // trace = new TraceReporter("res/multi-odin/result.trace");

            // SNN
            float alpha = (float)Math.Exp(-1.0 * 1.0 / 10.0);
            float beta = 1 - alpha;
            snn = new SNN();
            var input = new InputLayer(new TensorFile("res/multi-odin/validation/input_0.csv"), name: "input");
            snn.AddLayer(input);
            var hidden1 = new RLIFLayer(
                WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat("res/multi-odin/validation/weights_i_2_h1_n.csv", headers: true), scale: beta),
                WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat("res/multi-odin/validation/weights_h1_2_h1_n.csv", headers: true), scale: beta),
                name: "hidden1"
            );
            var hidden1Conv = RLIF2LIF(hidden1);
            hidden1Conv.leakage = alpha;
            hidden1Conv.threshold = 0.01f;
            hidden1Conv.resetMode = ResetMode.Subtract;
            // WeigthsUtil.ToCSV("res/multi-odin/test-weights.csv", hidden1Conv.weights);
            snn.AddLayer(hidden1Conv);

            var hidden2 = new RLIFLayer(
                WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat("res/multi-odin/validation/weights_h1_2_h2_n.csv", headers: true), scale: beta),
                WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat("res/multi-odin/validation/weights_h2_2_h2_n.csv", headers: true), scale: beta),
                name: "hidden2"
            );
            var hidden2Conv = RLIF2LIF(hidden2);
            hidden2Conv.leakage = alpha;
            hidden2Conv.threshold = 0.01f;
            hidden2Conv.resetMode = ResetMode.Subtract;
            snn.AddLayer(hidden2Conv);

            var output = new LIFLayer(
                WeigthsUtil.ReadFromCSVFloat("res/multi-odin/validation/weights_h2o_n.csv", headers: true),
                name: "output"
            );
            snn.AddLayer(output);

            tensor = new TensorReporter(snn, "res/multi-odin/tensor");
            tensor.RegisterLayer(hidden1Conv);
            tensor.RegisterLayer(hidden2Conv);
            tensor.RegisterLayer(output);

            mem = new MemReporter(snn, "res/multi-odin/mem");
            mem.RegisterLayer(hidden1Conv);
            mem.RegisterLayer(hidden2Conv);
            mem.RegisterLayer(output);

            // Hardware
            int width = 3;
            int height = 2;
            var delayModel = new ODINDelayModel
            {
                InputTime = 7,
                ComputeTime = 2,
                OutputTime = 8,
                TimeRefTime = 2
            };

            routers = MeshUtils.CreateMesh(sim, width, height, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));

            var controller = AddController(snn, 0, 0);
            controller.RLIF2LIF(hidden1, hidden1Conv);
            controller.RLIF2LIF(hidden2, hidden2Conv);
            var (core1, coreNi1) = AddCore(controller, delayModel, 1024, 0, 1, "core1");
            var (core2, coreNi2) = AddCore(controller, delayModel, 1024, 1, 1, "core2");
            var (core3, coreNi3) = AddCore(controller, delayModel, 1024, 2, 1, "core3");

            // Mapping
            var mapper = new FirstFitMapper(snn, new Core[] { controller, core1, core2, core3 });
            mapper.OnMappingFound += (core, layer) =>
            {
                if (core is ODINCore)
                {
                    controller.LayerToCoord(layer, (MeshCoord)core.GetLocation());
                }
            };
            mapper.Run();

            simStop.StopEvents = 10_000_000;
        }

        public override void Cleanup()
        {
            // trace.Finish();
            tensor.Finish();
            mem.Finish();
            Console.WriteLine($"Nr spikes: {tensor.NrSpikes}");
        }
    }
}