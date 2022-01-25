using System;

namespace SpikingDSE;

public class MeshTest : Experiment
{
    class Reporter
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
        var producer = sim.AddActor(new Producer(4, () => new MeshFlit { Dest = new MeshCoord(1, 1), Message = "hi" }, name: "producer"));
        producer.Produced += reporter.Produced;
        var consumer = sim.AddActor(new Consumer(name: "consumer"));
        consumer.Consumed += reporter.Consumed;
        var routers = MeshUtils.CreateMesh(sim, 2, 2, (x, y) => new SimpleXYRouter(x, y, 1, name: $"router({x},{y})"));

        sim.AddChannel(routers[0, 0].inLocal, producer.output);
        sim.AddChannel(routers[1, 1].outLocal, consumer.In);

        simStop.StopTime = 10;
    }

    public override void Cleanup()
    {

    }
}
