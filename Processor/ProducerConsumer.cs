using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE
{
    public class Producer : Process
    {
        public OutPort Out;

        private int interval;
        private object message;

        public delegate object Transform(object message);

        public Producer(int interval, object message, Transform transformer = null)
        {
            this.interval = interval;
            this.message = message;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                yield return env.Send(Out, message);
                yield return env.Delay(interval);
            }
        }
    }

    public class Consumer : Process
    {
        public InPort In;

        public Consumer()
        {

        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                yield return env.Receive(In);
            }
        }
    }
}