using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class Bus : Actor
{
    public InPort[] Inputs;
    public OutPort[] Outputs;

    public Bus(BusSpec spec, string name = "Bus")
    {
        this.Name = name;

        this.Inputs = new InPort[spec.Ports];
        this.Outputs = new OutPort[spec.Ports];
        for (int i = 0; i < spec.Ports; i++)
        {
            this.Inputs[i] = new();
            this.Outputs[i] = new();
        }
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        var anyInput = Any.AnyOf<Packet>(env, Inputs);

        while (true)
        {
            yield return anyInput.RequestRead();
            var packet = anyInput.Read().Message;
            anyInput.ReleaseRead();

            int dest = (int)packet.Dest;
            yield return env.Send(Outputs[dest], packet);
        }
    }
}