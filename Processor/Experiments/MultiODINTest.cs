using System;

namespace SpikingDSE
{
    public class MultiODINTest : Experiment
    {
        private TraceReporter reporter;

        public override void Setup()
        {
            reporter = new TraceReporter("res/multi-odin/result.trace");

            var mapping = new MeshMapping(sim);

            // SNN
            mapping.AddInputLayer(new InputLayer(EventTraceReader.ReadInputs("res/multi-odin/validation.trace"), name: "input"));
            var hidden1 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden1");
            mapping.AddHiddenLayer(hidden1);
            var hidden2 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden2");
            mapping.AddHiddenLayer(hidden2);
            var hidden3 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden3");
            mapping.AddHiddenLayer(hidden3);
            var hidden4 = new ODINLayer(WeigthsUtil.ReadFromCSV("res/multi-odin/weights_256.csv"), name: "hidden4");
            mapping.AddHiddenLayer(hidden4);

            // Hardware
            mapping.CreateMesh(3, 2);
            var delayModel = new ODINDelayModel
            {
                InputTime = 7,
                ComputeTime = 2,
                OutputTime = 8
            };
            mapping.AddCore(0, 0, new ODINCore(256, delayModel, name: "odin1"));
            mapping.AddCore(0, 1, new ODINCore(256, delayModel, name: "odin2"));
            mapping.AddCore(1, 0, new ODINCore(256, delayModel, name: "odin3"));
            mapping.AddCore(1, 1, new ODINCore(256, delayModel, name: "odin4"));
            mapping.AddSource(2, 1, new SpikeSourceTrace(name: "source"));
            mapping.AddSink(2, 0, new SpikeSink(name: "sink", reporter: reporter));

            // Compile
            mapping.Compile();
        }

        public override void Cleanup()
        {
            reporter.Cleanup();
        }
    }
}