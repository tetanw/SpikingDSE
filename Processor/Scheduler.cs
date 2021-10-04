using System;

namespace SpikingDSE
{
    public class EventScheduler
    {
        protected PriorityQueue<Event> events;

        public EventScheduler()
        {
            events = new PriorityQueue<Event>();
        }

        public void Schedule(Event ev)
        {
            events.Enqueue(ev);
        }

        public Event PopEvent()
        {
            return events.Dequeue();
        }

        public bool IsDone()
        {
            return events.Count == 0;
        }
    }

}