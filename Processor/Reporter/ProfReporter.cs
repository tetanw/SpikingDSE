using System.Collections.Generic;

namespace SpikingDSE
{
    public class ProfReporter : IReporter
    {
        private TraceWriter trace;

        private long neurEvStartTime = -1;
        private long eventInStartTime = -1;
        private int computingNeuronID = -1;

        private Dictionary<int, long> eventOutStarts = new Dictionary<int, long>();

        public ProfReporter(string path)
        {
            this.trace = new TraceWriter(path);
        }

        private void WriteEvent(long from, long to, string name)
        {
            const long CYLCES_TO_NS = 10;
            trace.WriteEvent(from * CYLCES_TO_NS, to * CYLCES_TO_NS, name);
        }

        public void Report(Event objEv)
        {
            if (objEv is StartSending)
            {
                var ev = (StartSending)objEv;
                eventOutStarts[ev.Synapse] = ev.Time;
            }
            else if (objEv is DoneSending)
            {
                var ev = (DoneSending)objEv;
                WriteEvent(eventOutStarts[ev.Synapse], objEv.Time, $"event-out {ev.Synapse}");
                eventOutStarts.Remove(ev.Synapse);
            }
            else if (objEv is StartReceiving)
            {
                var ev = (StartReceiving)objEv;
                eventInStartTime = ev.Time;
            }
            else if (objEv is DoneReceiving)
            {
                var ev = (DoneReceiving)objEv;
                WriteEvent(eventInStartTime, ev.Time, $"event-in {ev.NeuronID}");
                eventInStartTime = -1;
            }
            else if (objEv is StartComputing)
            {
                var ev = (StartComputing)objEv;
                neurEvStartTime = ev.Time;
                computingNeuronID = ev.NeuronID;
            }
            else if (objEv is DoneComputing)
            {
                var ev = (DoneComputing)objEv;
                WriteEvent(neurEvStartTime, ev.Time, $"neuron-event {computingNeuronID}");
                computingNeuronID = -1;
                neurEvStartTime = -1;
            }
        }

        public void Start()
        {
            trace.Start();
        }

        public void End(long time)
        {
            trace.Stop();
        }
    }
}