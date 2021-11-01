using System.Collections.Generic;

namespace SpikingDSE
{
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

        public override IEnumerable<Event> Run(Environment env)
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

}