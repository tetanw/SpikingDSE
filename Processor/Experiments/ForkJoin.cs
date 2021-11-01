namespace SpikingDSE
{
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
}