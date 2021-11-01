namespace SpikingDSE
{
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
}