using System.Collections.Generic;

namespace SpikingDSE
{
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

        public override IEnumerable<Event> Run(Environment env)
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