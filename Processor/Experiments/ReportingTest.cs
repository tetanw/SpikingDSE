using System;

namespace SpikingDSE
{
    public class ReportingTest : Experiment
    {
        public class Reporter : ProducerReport, ConsumerReporter
        {
            public void Consumed(Consumer consumer, long time, object message)
            {
                Console.WriteLine($"[{time}][Consumer] Received message: {message}");
            }

            public void Produced(Producer producer, long time, object message)
            {
                Console.WriteLine($"[{time}][Producer] Produced message: {message}");
            }
        }

        public override void Setup()
        {
            var reporter = new Reporter();

            var producer = sim.AddActor(new Producer(4, () => "hi", reporter: reporter));
            var consumer = sim.AddActor(new Consumer(reporter: reporter));

            sim.AddChannel(ref producer.Out, ref consumer.In);
            simStop.StopTime = 10;
        }

        public override void Cleanup()
        {

        }
    }

}