using System;

namespace SpikingDSE
{
    public class MultiODINTest : Experiment
    {
        private TraceReporter reporter;

        public override void Setup()
        {
            reporter = new TraceReporter("res/multi-odin/result.trace");

            // SNN
            var snn = new SNN();
            snn.AddInputLayer(new InputLayer(128, EventTraceReader.ReadInputs("res/multi-odin/validation.trace"), name: "input"));
            var hidden1 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden1");
            snn.AddHiddenLayer(hidden1);
            // var hidden2 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden2");
            // mapping.AddHiddenLayer(hidden2);
            // var hidden3 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden3");
            // mapping.AddHiddenLayer(hidden3);
            // var hidden4 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden4");
            // mapping.AddHiddenLayer(hidden4);

            // Hardware
            var mesh = new Mesh(sim, 3, 2, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));
            var delayModel = new ODINDelayModel
            {
                InputTime = 7,
                ComputeTime = 2,
                OutputTime = 8
            };
            mesh.AddActor(0, 0, new ODINCore(256, delayModel, name: "odin1"));
            // mapping.AddCore(0, 1, new ODINCore(256, delayModel, name: "odin2"));
            // mapping.AddCore(1, 0, new ODINCore(256, delayModel, name: "odin3"));
            // mapping.AddCore(1, 1, new ODINCore(256, delayModel, name: "odin4"));
            var controller = mesh.AddActor(2, 1, new Controller(name: "source"));
            controller.SpikeReceived += reporter.SpikeReceived;
            controller.SpikeSent += reporter.SpikeSent;

            // Compile
            var mapper = new MeshFirstFitMapper();
            mapper.Compile(mesh, snn);
        }

        public override void Cleanup()
        {
            reporter.Cleanup();
        }
    }
}