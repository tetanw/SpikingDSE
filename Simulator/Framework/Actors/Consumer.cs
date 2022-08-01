using System;
using System.Collections.Generic;

namespace SpikingDSE;

public delegate void Consumed(Consumer consumer, long time, object message);

public sealed class Consumer : Actor
{
    public Consumed Consumed;

    public InPort In = new();

    private readonly int interval;

    public Consumer(string name = "", int interval = 0)
    {
        this.interval = interval;
        this.Name = name;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        ReceiveEvent rcv;
        while (true)
        {
            yield return env.Delay(interval);
            rcv = env.Receive(In);
            yield return rcv;
            Consumed?.Invoke(this, env.Now, rcv.Message);
        }
    }
}
