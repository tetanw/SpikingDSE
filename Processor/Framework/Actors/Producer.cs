using System;
using System.Collections.Generic;

namespace SpikingDSE;


public sealed class Producer : Actor
{
    public delegate void Produced(Producer producer, long time, object message);
    public Produced WhenProduced;

    public OutPort output = new OutPort();

    private int interval;
    private Func<object> create;

    public Producer(int interval, Func<object> create, string name = "")
    {
        this.interval = interval;
        this.create = create;
        this.Name = name;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        while (true)
        {
            var message = create();
            yield return env.Send(output, message);
            WhenProduced?.Invoke(this, env.Now, message);
            yield return env.Delay(interval);
        }
    }
}