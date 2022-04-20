using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpikingDSE;

public class SimStopConditions
{
    public long StopTime = long.MaxValue;
    public long StopEvents = long.MaxValue;
}

public abstract class Experiment
{
    public object Context { get; set; }
    protected Simulator sim;
    protected SimStopConditions simStop;

    public Experiment()
    {
        this.sim = new Simulator();
        simStop = new SimStopConditions();
    }

    public bool Debug { get; set; } = true;

    public void PrintLn(string text)
    {
        if (Debug) Console.WriteLine(text);
    }

    public void Run()
    {
        Setup();
        sim.Compile();
        PrintLn("Simulation starting");
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        sim.RunUntil(simStop.StopTime, simStop.StopEvents);
        stopwatch.Stop();
        Cleanup();

        PrintLn("Simulation done");
        if (Debug)
        {
            sim.PrintDeadlockReport();
            sim.PrintActorReport();
        }
        PrintLn($"Simulation was stopped at time: {sim.Now:n}");
        PrintLn($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
        PrintLn($"Events handled: {sim.NrEventsProcessed:n}");
        PrintLn($"Performance was about: {sim.NrEventsProcessed / stopwatch.Elapsed.TotalSeconds:n} event/s");
        if (sim.NrEventsProcessed > 0)
        {
            PrintLn($"Time per event: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / sim.NrEventsProcessed, "s")}");
        }

    }

    public abstract void Setup();
    public abstract void Cleanup();
}

public abstract class BatchExperiment<T>
    where T : Experiment
{
    public void Run()
    {
        var m = new System.Threading.Mutex();


        var batchTime = new Stopwatch();
        batchTime.Start();

        var exp = Exp();
        Parallel.ForEach(exp, (exp, _, j) =>
         {
             var sampleTime = new Stopwatch();
             sampleTime.Start();
             exp.Run();
             sampleTime.Stop();

             m.WaitOne();
             WhenSampleDone(exp, j, sampleTime.Elapsed);
             m.ReleaseMutex();
         });

        batchTime.Stop();
        WhenCompleted(batchTime.Elapsed);

    }

    public abstract IEnumerable<T> Exp();

    public abstract void WhenSampleDone(T exp, long j, TimeSpan sampleTime);

    public abstract void WhenCompleted(TimeSpan batchTime);
}