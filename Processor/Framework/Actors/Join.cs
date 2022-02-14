using System.Collections.Generic;

namespace SpikingDSE
{
    public interface JoinReporter
    {

    }

    public sealed class Join : Actor
    {
        public InPort in1 = new InPort();
        public InPort in2 = new InPort();
        public InPort in3 = new InPort();
        public OutPort output = new OutPort();

        private JoinReporter reporter;

        public Join(JoinReporter reporter = null)
        {
            this.reporter = reporter;
        }

        public override IEnumerable<Event> Run(Simulator env)
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