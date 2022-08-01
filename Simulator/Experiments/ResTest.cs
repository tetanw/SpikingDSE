using System;

namespace SpikingDSE;

public class ResTest : Experiment
{

    public static void Consumed(Consumer consumer, long time, object message)
    {
        Console.WriteLine($"[{time}] consumer received {message}");
    }

    public static void Produced(Producer producer, long time, object message)
    {
        Console.WriteLine($"[{time}] producer sent {message}");
    }

    public override void Setup()
    {
        var producer = sim.AddActor(new Producer(0, () => "Hi", name: "producer"));
        producer.WhenProduced = Produced;
        var buffer = sim.AddActor(new BufferActor(5));
        var consumer = sim.AddActor(new Consumer(interval: 3, name: "consumer"));
        consumer.Consumed = Consumed;

        sim.AddChannel(producer.output, buffer.input);
        sim.AddChannel(buffer.output, consumer.In);

        simStop.StopTime = 100;
    }

    public override void Cleanup()
    {

    }

}
