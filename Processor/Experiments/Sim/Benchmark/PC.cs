using System;

namespace SpikingDSE;

public class ProducerConsumer : Experiment
{
    public override void Setup()
    {
        var producer = sim.AddActor(new Producer(8, () => "hi", name: "P1"));
        var consumer = sim.AddActor(new Consumer(name: "C1"));

        sim.AddChannel(consumer.In, producer.output);

        simStop.StopEvents = 10_000_000;
    }

    public override void Cleanup()
    {

    }

}
