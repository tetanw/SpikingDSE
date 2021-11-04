namespace SpikingDSE
{
    public class ODINSingleCore : Experiment
    {
        private TraceReporter reporter;

        public override void Setup()
        {
            reporter = new TraceReporter("res/odin/result.trace");
            var input = sim.AddActor(new SpikeSourceTrace(startTime: 4521));
            input.SpikeSent = reporter.SpikeSent;
            var inLayer = new InputLayer(128, EventTraceReader.ReadInputs("res/odin/validation.trace"));
            input.LoadLayer(inLayer);
            var output = sim.AddActor(new SpikeSink());
            output.SpikeReceived += reporter.SpikeReceived;

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

            sim.AddChannel(core1.spikesIn, input.spikesOut);
            sim.AddChannel(output.spikesIn, core1.spikesOut);
        }

        public override void Cleanup()
        {
            reporter.Cleanup();
        }
    }
}