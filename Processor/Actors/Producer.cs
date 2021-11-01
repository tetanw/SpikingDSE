using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    public interface ProducerReport
    {
        public void Produced(Producer producer, long time, object message);
    }

    public class Producer : Actor
    {
        public OutPort Out;

        private int interval;
        private ProducerReport reporter;
        private Func<object> create;

        public Producer(int interval, Func<object> create, string name = "", ProducerReport reporter = null)
        {
            this.interval = interval;
            this.create = create;
            this.Name = name;
            this.reporter = reporter;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            while (true)
            {
                var message = create();
                yield return env.Send(Out, message);
                reporter?.Produced(this, env.Now, message);
                yield return env.Delay(interval);
            }
        }
    }
}