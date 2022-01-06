using System;
using System.Diagnostics;
using System.IO;

namespace SpikingDSE;

public class SimStopConditions
{
    public long StopTime = long.MaxValue;
    public long StopEvents = long.MaxValue;
}

public abstract class Experiment
{
    protected Simulator sim;
    protected SimStopConditions simStop;

    public Experiment()
    {
        sim = new Simulator();
        simStop = new SimStopConditions();
    }

    public void Run()
    {
        Setup();

        sim.Compile();
        Console.WriteLine("Simulation starting");
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        var (time, nrEvents) = sim.RunUntil(simStop.StopTime, simStop.StopEvents);
        stopwatch.Stop();

        Cleanup();

        Console.WriteLine("Simulation done");
        sim.PrintDeadlockReport();
        Console.WriteLine($"Simulation was stopped at time: {time:n}");
        Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Events handled: {nrEvents:n}");
        Console.WriteLine($"Performance was about: {nrEvents / stopwatch.Elapsed.TotalSeconds:n} event/s");
        if (nrEvents > 0)
        {
            Console.WriteLine($"Time per event: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrEvents, "s")}");
        }
    }

    public abstract void Setup();
    public abstract void Cleanup();
}