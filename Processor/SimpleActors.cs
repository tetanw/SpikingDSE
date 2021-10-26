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
        private object message;
        private ProducerReport reporter;

        public Producer(int interval, object message, string name = "", ProducerReport reporter = null)
        {
            this.Name = name;
            this.interval = interval;
            this.message = message;
            this.reporter = reporter;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
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
        private int interval;

        public Consumer(string name = "", int interval = 0, ConsumerReporter reporter = null)
        {
            this.reporter = reporter;
            this.interval = interval;
            this.Name = name;
        }

        public override IEnumerable<Command> Run()
        {
            ReceiveCmd rcv;
            while (true)
            {
                rcv = env.Receive(In, waitBefore: interval);
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

    public class FIFO : Actor
    {
        public InPort input;
        public OutPort output;

        private int depth;
        private Queue<object> items = new Queue<object>();
        private Resource bufferFree;
        private Resource bufferFilled;

        public FIFO(int depth)
        {
            this.depth = depth;
        }

        public override IEnumerable<Command> Run()
        {
            var bufferFreeCreate = env.CreateResource(depth);
            yield return bufferFreeCreate;
            bufferFree = bufferFreeCreate.Resource;

            var bufferFilledCreate = env.CreateResource(0);
            yield return bufferFilledCreate;
            bufferFilled = bufferFilledCreate.Resource;

            yield return env.Parallel(
                Send(),
                Receive()
            );
        }

        public IEnumerable<Command> Send()
        {
            while (true)
            {
                yield return env.Decrease(bufferFilled, 1);
                yield return env.Send(output, items.Dequeue());
                yield return env.Increase(bufferFree, 1);
            }
        }

        public IEnumerable<Command> Receive()
        {
            while (true)
            {
                yield return env.Decrease(bufferFree, 1);
                var rcv = env.Receive(input);
                yield return rcv;
                items.Enqueue(rcv.Message);
                yield return env.Increase(bufferFilled, 1);
            }
        }
    }
}