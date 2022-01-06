namespace SpikingDSE;

public class ForkJoin : Experiment
{
    public override void Setup()
    {
        var producer = sim.AddActor(new Producer(8, () => "hi"));
        var consumer = sim.AddActor(new Consumer());
        var fork = sim.AddActor(new Fork());
        var join = sim.AddActor(new Join());

        sim.AddChannel(producer.output, fork.input);
        sim.AddChannel(fork.out1, join.in1);
        sim.AddChannel(fork.out2, join.in2);
        sim.AddChannel(fork.out3, join.in3);
        sim.AddChannel(join.output, consumer.In);

        simStop.StopEvents = 100;
    }

    public override void Cleanup()
    {

    }
}