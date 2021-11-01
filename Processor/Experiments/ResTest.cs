using System;

namespace SpikingDSE
{
    public class ResTest : Experiment
    {
        class Reporter : ProducerReport, ConsumerReporter
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
            var producer = sim.AddActor(new Producer(0, () => "Hi", name: "producer", reporter: reporter));
            var buffer = sim.AddActor(new Buffer(5));
            var consumer = sim.AddActor(new Consumer(interval: 3, name: "consumer", reporter: reporter));

            sim.AddChannel(ref producer.Out, ref buffer.input);
            sim.AddChannel(ref buffer.output, ref consumer.In);

            simStop.StopTime = 100;
        }

        public override void Cleanup()
        {

        }

    }
}