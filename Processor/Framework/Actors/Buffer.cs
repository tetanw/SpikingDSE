using System.Collections.Generic;

namespace SpikingDSE
{
    public sealed class BufferActor : Actor
    {
        public InPort input = new InPort();
        public OutPort output = new OutPort();

        private int depth;
        private Buffer<object> fifo;

        public BufferActor(int depth)
        {
            this.depth = depth;
        }

        public override IEnumerable<Event> Run(Simulator env)
        {
            fifo = new Buffer<object>(env, depth);
            env.Process(Send(env));
            env.Process(Receive(env));
            yield break;
        }

        public IEnumerable<Event> Send(Simulator env)
        {
            while (true)
            {
                yield return fifo.RequestRead();
                var message = fifo.Read();
                yield return env.Send(output, message);
                fifo.ReleaseRead();
            }
        }

        public IEnumerable<Event> Receive(Simulator env)
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