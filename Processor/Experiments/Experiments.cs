using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

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

    public Experiment(Simulator sim)
    {
        this.sim = sim;
        simStop = new SimStopConditions();
    }

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
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        var (time, nrEvents) = sim.RunUntil(simStop.StopTime, simStop.StopEvents);
        stopwatch.Stop();

        Cleanup();

        PrintLn("Simulation done");
        if (Debug) sim.PrintDeadlockReport();
        PrintLn($"Simulation was stopped at time: {time:n}");
        PrintLn($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
        PrintLn($"Events handled: {nrEvents:n}");
        PrintLn($"Performance was about: {nrEvents / stopwatch.Elapsed.TotalSeconds:n} event/s");
        if (nrEvents > 0)
        {
            PrintLn($"Time per event: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrEvents, "s")}");
        }

    }

    public abstract void Setup();
    public abstract void Cleanup();
}

public abstract class DSEExperiment<T>
    where T : Experiment
{
    public void Run()
    {
        var m = new Mutex();

        foreach (var config in Configs())
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.ForEach(config, (exp, _, j) =>
             {
                 exp.Run();

                 m.WaitOne();
                 OnExpCompleted(exp);
                 m.ReleaseMutex();
             });

            sw.Stop();
            OnConfigCompleted(sw.Elapsed);
        }
    }

    public abstract IEnumerable<IEnumerable<T>> Configs();

    public abstract void OnExpCompleted(T exp);

    public abstract void OnConfigCompleted(TimeSpan runningTime);
}