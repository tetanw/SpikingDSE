using System.Collections.Generic;

namespace SpikingDSE
{
    public class Buffer : Actor
    {
        public InPort input = new InPort();
        public OutPort output = new OutPort();

        private int depth;
        private FIFO<object> fifo;

        public Buffer(int depth)
        {
            this.depth = depth;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            fifo = new FIFO<object>(env, depth);
            env.Process(Send(env));
            env.Process(Receive(env));
            yield break;
        }

        public IEnumerable<Event> Send(Environment env)
        {
            while (true)
            {
                yield return fifo.RequestRead();
                var message = fifo.Read();
                yield return env.Send(output, message);
                fifo.ReleaseRead();
            }
        }

        public IEnumerable<Event> Receive(Environment env)
        {
            while (true)
            {
                yield return fifo.RequestWrite();
                var rcv = env.Receive(input);
                yield return rcv;
                fifo.Write(rcv.Message);
                fifo.ReleaseWrite();
            }
        }
    }
}