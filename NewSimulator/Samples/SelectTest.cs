using System.Diagnostics;

namespace NewSimulator;

public class Producer
{
    public string Name;
    public Channel Output;
    public long Delay;

    public Producer(string name, Channel output, long delay)
    {
        this.Name = name;
        this.Output = output;
        this.Delay = delay;
    }

    public IEnumerable<Event> Run(Simulator env)
    {
        yield return env.Delay(this.Delay);
        for (int i = 0; i < 3; i++)
        {
            yield return env.Send(Output, $"[{Name}] hi {i}");
        }
    }
}

public class Consumer
{
    public Channel[] Inputs;

    public Consumer(params Channel[] inputs)
    {
        this.Inputs = inputs;
    }

    public IEnumerable<Event> Run(Simulator env)
    {
        while (true)
        {
            var readEvs = new Event[Inputs.Length];
            for (int i = 0; i < readEvs.Length; i++)
            {
                readEvs[i] = env.Receive(Inputs[i]);
            }

            // yield return env.AnyOf(readEvs);
            Console.WriteLine("Any of");
        }
    }
}



public class SelectTest
{
    public void Run()
    {
        var sim = new Simulator();
        var p1toC = new Channel("p1toc");
        var p2toC = new Channel("p2toc");
        var p3toC = new Channel("p3toc");
        var p1 = new Producer("p1", p1toC, 1);
        var p2 = new Producer("p2", p2toC, 2);
        var p3 = new Producer("p3", p3toC, 3);
        var c = new Consumer(p1toC, p2toC, p3toC);
        sim.AddProcess(p1.Run(sim));
        sim.AddProcess(p2.Run(sim));
        sim.AddProcess(p3.Run(sim));
        sim.AddProcess(c.Run(sim));
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