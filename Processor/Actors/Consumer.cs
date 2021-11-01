using System.Collections.Generic;

namespace SpikingDSE
{
    public interface ConsumerReporter
    {
        public void Consumed(Consumer consumer, long time, object message);
    }

    public class Consumer : Actor
    {
        public InPort In;

        private ConsumerReporter reporter;
        private int interval;

        public Consumer(string name = "", int interval = 0, ConsumerReporter reporter = null)
        {
            this.reporter = reporter;
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
                reporter?.Consumed(this, env.Now, rcv.Message);
            }
        }
    }
}