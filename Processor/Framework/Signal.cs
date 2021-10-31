namespace SpikingDSE
{
    public sealed class Signal
    {
        private Resource res;

        public Signal(Environment env)
        {
            res = env.CreateResource(1);
        }

        public void Notify(Environment env)
        {
            env.IncreaseResource(res, 1);
        }

        public Event Wait(Environment env)
        {
            return env.RequestResource(res, 1);
        }
    }
}