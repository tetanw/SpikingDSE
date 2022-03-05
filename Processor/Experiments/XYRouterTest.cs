using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class XYRouterTest : Experiment
{
    public override void Setup()
    {
        int i = 0;
        var southProd = sim.AddActor(new Producer(20, () => new Packet { Src = new MeshCoord(-1, -1), Dest = new MeshCoord(1, 1), Message = ("South", i++) }, "South"));

        int j = 0;
        var northProd = sim.AddActor(new Producer(15, () => new Packet { Src = new MeshCoord(-1, -1), Dest = new MeshCoord(1, 1), Message = ("North", j++) }, "North"));

        var router = sim.AddActor(new ProtoXYRouter(0, 1, name: "Router"));

        var consumer = sim.AddActor(new Consumer(interval: 50, name: "Consumer"));
        consumer.Consumed += (_, time, flit) =>
        {
            var message = ((Packet)flit).Message;
            Console.WriteLine($"[{time}] {message}");
        };

        sim.AddChannel(southProd.output, router.inSouth);
        sim.AddChannel(northProd.output, router.inNorth);
        sim.AddChannel(router.outEast, consumer.In);

        simStop.StopTime = 1000;
    }

    public override void Cleanup()
    {

    }
}
