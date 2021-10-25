using System;
using System.Collections.Generic;
using System.Linq;

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

        public override IEnumerable<Command> Run()
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

    public interface ConsumerReporter
    {
        public void Consumed(Consumer consumer, long time, object message);
    }

    public class Consumer : Actor
    {
        public InPort In;

        private ConsumerReporter reporter;

        public Consumer(string name = "", ConsumerReporter reporter = null)
        {
            this.reporter = reporter;
            this.Name = name;
        }

        public override IEnumerable<Command> Run()
        {
            ReceiveCmd rcv;
            while (true)
            {
                rcv = env.Receive(In);
                yield return rcv;
                reporter?.Consumed(this, env.Now, rcv.Message);
            }
        }
    }

    public interface ForkReporter
    {
        public void MessageSent(Fork fork, OutPort port, object message);
    }

    public class Fork : Actor
    {
        public InPort input;
        public OutPort out1;
        public OutPort out2;
        public OutPort out3;

        private ForkReporter reporter;

        public Fork(ForkReporter reporter = null)
        {
            this.reporter = reporter;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                var recv = env.Receive(input);
                yield return recv;
                var message = recv.Message;

                yield return env.Send(out1, message);
                reporter?.MessageSent(this, out1, message);
                yield return env.Send(out2, message);
                reporter?.MessageSent(this, out2, message);
                yield return env.Send(out3, message);
                reporter?.MessageSent(this, out3, message);
            }
        }
    }

    public interface JoinReporter
    {

    }

    public class Join : Actor
    {
        public InPort in1;
        public InPort in2;
        public InPort in3;
        public OutPort output;

        private JoinReporter reporter;

        public Join(JoinReporter reporter = null)
        {
            this.reporter = reporter;
        }

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