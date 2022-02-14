using System.Diagnostics;

namespace NewSimulator;

class Waiter
{
    public IEnumerable<Event> Run(Simulator env)
    {
        for (int i = 0; i < 5; i++)
        {
            yield return env.Process(Item(env));
            Console.WriteLine($"[{env.Now}] parent");
        }
    }

    public IEnumerable<Event> Item(Simulator env)
    {
        Console.WriteLine($"[{env.Now}] child");
        yield return env.Delay(3);
    }
}


public class ProcessTest
{
    public void Run()
    {
        var sim = new Simulator();
        var test = new Waiter();
        sim.AddProcess(test.Run(sim));
        var running = new Stopwatch();
        running.Start();
        sim.Run();
        running.Stop();
        Console.WriteLine($"#events elapsed: {sim.NrEventsProcessed:n}");
        Console.WriteLine($"Running time: {running.ElapsedMilliseconds:n} ms");
        Console.WriteLine($"Events/sec: {(sim.NrEventsProcessed / running.ElapsedMilliseconds * 1000):n}");
        Console.WriteLine($"Time per event: {(running.Elapsed.TotalSeconds / sim.NrEventsProcessed * 1_000_000_000):n} ns");
    }
}