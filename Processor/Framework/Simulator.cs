namespace SpikingDSE
{
    public sealed class Simulator
    {
        private Scheduler scheduler;

        public Simulator()
        {
            this.scheduler = new Scheduler();
        }

        public Resource CreateResource(int initial)
        {
            return scheduler.CreateResource(initial);
        }

        public void AddChannel(ref InPort inPort, ref OutPort outPort)
        {
            scheduler.AddChannel(ref inPort, ref outPort);
        }

        public void AddChannel(ref OutPort outPort, ref InPort inPort)
        {
            scheduler.AddChannel(ref outPort, ref inPort);
        }

        public T AddActor<T>(T process) where T : Actor
        {
            return scheduler.AddActor(process);
        }

        public void Compile()
        {
            scheduler.Compile();
        }

        public (long time, long nrEvents) RunUntil(long stopTime = long.MaxValue, long stopEvents = long.MaxValue)
        {
            return scheduler.RunUntil(stopTime, stopEvents);
        }

        public void PrintDeadlockReport()
        {
            scheduler.PrintDeadlockReport();
        }
    }
}