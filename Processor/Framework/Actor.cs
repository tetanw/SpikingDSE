using System.Collections.Generic;

namespace SpikingDSE
{
    public abstract class Actor
    {
        protected Environment env;
        public string Name { get; protected set; }

        public void Init(Environment env)
        {
            this.env = env;
        }

        public abstract IEnumerable<Event> Run();
    }
}