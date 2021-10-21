using System.Collections.Generic;

namespace SpikingDSE
{
        public class Producer : Process
    {
        public OutPort Out;

        private int interval;
        private object message;

        public Producer(int interval, object message)
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
            while(true)
            {
                yield return env.Receive(In);
            }
        }
    }
}