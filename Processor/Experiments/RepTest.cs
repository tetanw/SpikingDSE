using System;

namespace SpikingDSE;

public class RepTest : Experiment
{

    public static void Consumed(Consumer consumer, long time, object message)
    {
        Console.WriteLine($"[{time}][Consumer] Received message: {message}");
    }

    public static void Produced(Producer producer, long time, object message)
    {
        Console.WriteLine($"[{time}][Producer] Produced message: {message}");
    }

    public override void Setup()
    {
        var producer = sim.AddActor(new Producer(4, () => "hi"));
        producer.WhenProduced += Produced;
        var consumer = sim.AddActor(new Consumer());
        consumer.Consumed += Consumed;

        sim.AddChannel(producer.output, consumer.In);
        simStop.StopTime = 10;
    }

    public override void Cleanup()
    {

    }
}