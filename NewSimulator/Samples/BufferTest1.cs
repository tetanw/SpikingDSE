using System.Diagnostics;

namespace NewSimulator;

public class BufferProducer1
{
    private Buffer<int> buffer;
    private int ID;

    public BufferProducer1(Buffer<int> buffer, int ID)
    {
        this.buffer = buffer;
        this.ID = ID;
    }

    public IEnumerable<Event?> Run(Simulator env)
    {
        for (int i = 0; i < 10; i++)
        {
            buffer.Write(ID);
            yield return env.Delay(2);
        }
    }
}

public class BufferConsumer1
{
    private Buffer<int> buffer;

    public BufferConsumer1(Buffer<int> buffer)
    {
        this.buffer = buffer;
    }

    public IEnumerable<Event?> Run(Simulator env)
    {
        for (int i = 0; i < 20; i++)
        {
            var item = buffer.Read();
            Console.WriteLine($"[{env.Now}] {item}");
            yield return env.Delay(1);
        }
    }
}

public class BufferTest1
{
    public void Run()
    {
        var sim = new Simulator();
        var buffer = new Buffer<int>(sim, 3);
        var prod1 = new BufferProducer1(buffer, 1);
        var prod2 = new BufferProducer1(buffer, 2);
        var cons = new BufferConsumer1(buffer);
        sim.AddProcess(prod1.Run(sim));
        sim.AddProcess(prod2.Run(sim));
        sim.AddProcess(cons.Run(sim));
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