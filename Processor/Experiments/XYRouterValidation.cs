using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class XYRouterValidation : Experiment
{
    public override void Setup()
    {
        int i = 0;
        var southProd = sim.AddActor(new Producer(5, () => new MeshPacket { Src = new MeshCoord(-1, -1), Dest = new MeshCoord(1, 1), Message = ("South", i++) }, "South"));

        int j = 0;
        var northProd = sim.AddActor(new Producer(10, () => new MeshPacket { Src = new MeshCoord(-1, -1), Dest = new MeshCoord(1, 1), Message = ("North", j++) }, "North"));

        var router = sim.AddActor(new XYRouter(0, 1, 3, 5, 8, name: "Router"));

        var consumer = sim.AddActor(new Consumer(interval: 3, name: "Consumer"));
        consumer.Consumed += (_, time, flit) =>
        {
            var message = ((MeshPacket)flit).Message;
            Console.WriteLine($"[{time}] {message}");
        };

        sim.AddChannel(southProd.output, router.inSouth);
        sim.AddChannel(northProd.output, router.inNorth);
        sim.AddChannel(router.outEast, consumer.In);

        simStop.StopEvents = 100;
    }

    public override void Cleanup()
    {

    }
}
