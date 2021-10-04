namespace SpikingDSE
{
    public abstract class Actor
    {
        protected int ThisID;
        protected EventScheduler events;

        public void Init(int thisID, EventScheduler events)
        {
            this.ThisID = thisID;
            this.events = events;
        }

        protected void Schedule(Event ev)
        {
            ev.FromID = ThisID;
            events.Schedule(ev);
        }

        public long CurrentTime { protected get; set; }

        public abstract void Handle(Event ev);
        public abstract void Start();
    }

}