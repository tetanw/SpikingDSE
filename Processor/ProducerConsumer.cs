using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE
{
    public class Producer : Actor
    {
        public OutPort Out;

        private int interval;
        private object message;

        public Producer(int interval, object message, string name = "")
        {
            this.name = name;
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

    public class Consumer : Actor
    {
        public InPort In;

        public Consumer(string name = "")
        {
            this.name = name;
        }

        public override IEnumerable<Command> Run()
        {
            ReceiveCmd rcv;
            while (true)
            {
                rcv = env.Receive(In);
                yield return rcv;
            }
        }
    }

    public class Fork : Actor
    {
        public InPort input;
        public OutPort out1;
        public OutPort out2;
        public OutPort out3;

        public Fork()
        {

        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                var recv = env.Receive(input);
                yield return recv;
                var message = recv.Message;

                yield return env.Send(out1, message);
                yield return env.Send(out2, message);
                yield return env.Send(out3, message);
            }
        }
    }

    public class Join : Actor
    {
        public InPort in1;
        public InPort in2;
        public InPort in3;
        public OutPort output;

        public override IEnumerable<Command> Run()
        {
            List<object> bundle = new List<object>();

            while (true)
            {
                var select = env.Select(in1, in2, in3);
                yield return select;
                var message = select.Message;

                bundle.Add(message);
                if (bundle.Count == 3)
                {
                    yield return env.Send(output, bundle.ToArray());
                    bundle.Clear();
                }
            }
        }
    }
}