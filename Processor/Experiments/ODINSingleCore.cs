namespace SpikingDSE
{
    public class ODINSingleCore : Experiment
    {
        private TraceReporter reporter;

        public override void Setup()
        {
            reporter = new TraceReporter("res/odin/result.trace");
            var controller = sim.AddActor(new Controller(startTime: 4521));
            controller.SpikeSent = reporter.SpikeSent;
            controller.SpikeReceived += reporter.SpikeReceived;
            var inLayer = new InputLayer(128, EventTraceReader.ReadInputs("res/odin/validation.trace"));
            controller.LoadLayer(inLayer);

            var delayModel = new ODINDelayModel
            {
                InputTime = 7,
                ComputeTime = 2,
                OutputTime = 8
            };
            var core1 = sim.AddActor(new ODINCore(256, delayModel, name: "odin1"));
            var weights = WeigthsUtil.ReadFromCSV("res/odin/weights_256.csv");
            var layer = new ODINLayer(weights, threshold: 30, name: "hidden");
            core1.AddLayer(layer);

            sim.AddChannel(core1.spikesIn, controller.spikesOut);
            sim.AddChannel(controller.spikesIn, core1.spikesOut);
        }

        public override void Cleanup()
        {
            reporter.Cleanup();
        }
    }
}