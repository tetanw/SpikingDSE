using System;
using System.Collections.Generic;

namespace SpikingDSE;

class ToyProducer : Actor
{
    public OutPort output = new();

    public ToyProducer(string name)
    {
        this.Name = name;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        while (true)
        {
            string message = Name;
            Console.WriteLine($"[{env.Now}] Producer {Name} sent message: {message}");
            yield return env.Send(output, message);
        }
    }
}

class ToyConsumer : Actor
{
    public InPort in1 = new();
    public InPort in2 = new();

    public override IEnumerable<Event> Run(Simulator env)
    {
        while (true)
        {
            yield return env.Delay(3);
            var rcv1 = env.Receive(in1);
            yield return rcv1;
            Console.WriteLine($"[{env.Now}] Received message from p1");

            yield return env.Delay(3);
            var rcv2 = env.Receive(in2);
            yield return rcv2;
            Console.WriteLine($"[{env.Now}] Received message from p2");
        }
    }
}

public class ToyProblem : Experiment
{
    public override void Setup()
    {
        var p1 = new ToyProducer("p1");
        var p2 = new ToyProducer("p2");
        var c1 = new ToyConsumer();
        sim.AddActor(p1);
        sim.AddActor(p2);
        sim.AddActor(c1);

        sim.AddChannel(p1.output, c1.in1);
        sim.AddChannel(p2.output, c1.in2);

        simStop.StopTime = 20;
    }

    public override void Cleanup()
    {

    }

}
