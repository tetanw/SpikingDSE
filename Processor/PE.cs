using System.Collections.Generic;

namespace SpikingDSE
{
    public class Scheduler
    {
        private int bufferSize;
        private Queue<int> buffer;

        public Scheduler(int bufferSize)
        {
            this.bufferSize = bufferSize;
            this.buffer = new Queue<int>(bufferSize);
        }

        public void PushNeuron(int neuronID)
        {
            buffer.Enqueue(neuronID);
        }

        public int PopNeuron()
        {
            return buffer.Dequeue();
        }

        public bool isSchedulerFull
        {
            get => buffer.Count == bufferSize;
        }

        public bool isSchedulerEmpty
        {
            get => buffer.Count == 0;
        }

        public int NrSpikesWaiting
        {
            get => buffer.Count;
        }
    }
}