using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class Bus : Actor
{
    public delegate void Transfer(long time, int from, int to);
    public Transfer OnTransfer;

    public InPort[] Inputs;
    public OutPort[] Outputs;
    private readonly BusSpec spec;

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
        this.spec = spec;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        var anyInput = Any.AnyOf<Packet>(env, Inputs);

        while (true)
        {
            yield return anyInput.RequestRead();
            var sel = anyInput.Read();
            anyInput.ReleaseRead();

            var packet = sel.Message;
            int dest = (int)packet.Dest;
            long before = env.Now;
            yield return env.Send(Outputs[dest], packet, transferTime: spec.TransferDelay);
            long after = env.Now;
            OnTransfer?.Invoke(env.Now, sel.PortNr, dest);
        }
    }
}