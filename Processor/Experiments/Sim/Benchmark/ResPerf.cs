namespace SpikingDSE;

public class ResPerf : Experiment
{
    public override void Setup()
    {
        var producer = sim.AddActor(new Producer(1, () => "Hi", name: "producer"));
        var fifo = sim.AddActor(new Buffer(1));
        var consumer = sim.AddActor(new Consumer(interval: 3, name: "consumer"));

        sim.AddChannel(producer.output, fifo.input);
        sim.AddChannel(fifo.output, consumer.In);

        simStop.StopEvents = 10_000_000;
    }

    public override void Cleanup()
    {

    }

}
