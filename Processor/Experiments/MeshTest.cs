using System;

namespace SpikingDSE
{
    public class MeshTest : Experiment
    {
        class Reporter : ConsumerReporter, ProducerReport
        {
            public void Consumed(Consumer consumer, long time, object message)
            {
                var realMessage = ((MeshFlit)message).Message;
                Console.WriteLine($"[{time}] {consumer.Name} received message {realMessage}");
            }

            public void Produced(Producer producer, long time, object message)
            {
                var realMessage = ((MeshFlit)message).Message;
                Console.WriteLine($"[{time}] {producer.Name} sent message {realMessage}");
            }
        }

        public override void Setup()
        {
            var reporter = new Reporter();
            var producer = sim.AddActor(new Producer(4, () => new MeshFlit { DestX = 1, DestY = 1, Message = "hi" }, name: "producer", reporter: reporter));
            var consumer = sim.AddActor(new Consumer(name: "consumer", reporter: reporter));
            var routers = MeshUtils.CreateMesh(sim, 2, 2, (x, y) => new XYRouter(x, y, 1, name: $"router({x},{y})"));

            sim.AddChannel(ref routers[0, 0].inLocal, ref producer.Out);
            sim.AddChannel(ref routers[1, 1].outLocal, ref consumer.In);

            simStop.StopTime = 10;
        }

        public override void Cleanup()
        {

        }
    }
}