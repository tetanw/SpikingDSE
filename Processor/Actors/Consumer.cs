using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    public delegate void Consumed(Consumer consumer, long time, object message);

    public class Consumer : Actor
    {
        public Consumed Consumed;

        public InPort In = new InPort();

        private int interval;

        public Consumer(string name = "", int interval = 0)
        {
            this.interval = interval;
            this.Name = name;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            ReceiveEvent rcv;
            while (true)
            {
                rcv = env.Receive(In, waitBefore: interval);
                yield return rcv;
                Consumed?.Invoke(this, env.Now, rcv.Message);
            }
        }
    }
}