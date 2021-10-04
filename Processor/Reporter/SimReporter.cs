using System;
using System.Diagnostics;

namespace SpikingDSE
{
    public class SimReporter : IReporter
    {
        public Stopwatch simTime = new Stopwatch();
        public int nrEventsHandled = 0;


        public void Report(Event objEv)
        {
            nrEventsHandled++;
        }

        public void Start()
        {
            simTime.Start();
        }

        public void End(long time)
        {
            simTime.Stop();
            double timePerEvent = simTime.ElapsedMilliseconds / 1000.0 / nrEventsHandled;
            Console.WriteLine($"Simulation:");
            Console.WriteLine($"  Time taken: {simTime.ElapsedMilliseconds:#,0} ms");
            Console.WriteLine($"  Nr events handled: {nrEventsHandled:#,0}");
            Console.WriteLine($"  Time per event: {Measurements.GetPrefix(timePerEvent)}s");
        }
    }
}