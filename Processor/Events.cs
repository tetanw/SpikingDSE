using System;

namespace SpikingDSE
{
    public abstract class Event : IComparable
    {
        public int TargetID = -1;
        public int FromID = -1;
        public long Time = -1;

        public int CompareTo(object obj)
        {
            if (obj == null) return 0;
            if (!(obj is Event)) return 0;

            var other = obj as Event;
            return this.Time.CompareTo(other.Time);
        }
    }

    public class StartReceiving : Event
    {

    }

    public class DoneReceiving : Event
    {
        public int NeuronID;
    }

    public class StartComputing : Event
    {
        public int NeuronID;
    }

    public class DoneComputing : Event
    {

    }

    public class ComputingSynapse : Event
    {
        public int Synapse;
    }

    public class StartSending : Event
    {
        public int Synapse;
    }

    public class DoneSending : Event
    {
        public int Synapse;
    }
}