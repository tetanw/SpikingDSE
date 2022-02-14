using System;

namespace SpikingDSE;

public class ResTest : Experiment
{
    class Reporter
    {
        public void Consumed(Consumer consumer, long time, object message)
        {
            Console.WriteLine($"[{time}] consumer received {message}");
        }

        public void Produced(Producer producer, long time, object message)
        {
            Console.WriteLine($"[{time}] producer sent {message}");
        }
    }

    public override void Setup()
    {
        var reporter = new Reporter();
        var producer = sim.AddActor(new Producer(0, () => "Hi", name: "producer"));
        producer.WhenProduced = reporter.Produced;
        var buffer = sim.AddActor(new BufferActor(5));
        var consumer = sim.AddActor(new Consumer(interval: 3, name: "consumer"));
        consumer.Consumed = reporter.Consumed;

        sim.AddChannel(producer.output, buffer.input);
        sim.AddChannel(buffer.output, consumer.In);

        simStop.StopTime = 100;
    }

    public override void Cleanup()
    {

    }

}
