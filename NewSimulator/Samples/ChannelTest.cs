using System.Diagnostics;

namespace NewSimulator;

class Person
{
    public IEnumerable<Event> Run(Simulator env, bool start, Channel to, Channel from)
    {
        if (start)
            yield return env.Send(to, 0);

        while (true)
        {
            yield return env.Delay(3);

            var rcv = env.Receive(from);
            yield return rcv;
            var count = (int)rcv.Value!;
            Console.WriteLine($"[{env.Now}] {count}");

            if (count < 10)
                yield return env.Send(to, count + 1);
        }
    }
}

public class ChannelTest
{
    public void Run()
    {
        var alice = new Person();
        var bob = new Person();
        var sim = new Simulator();
        var AToB = new Channel("AToB");
        var BToA = new Channel("BToA");
        sim.AddProcess(alice.Run(sim, true, AToB, BToA));
        sim.AddProcess(bob.Run(sim, false, BToA, AToB));
        var running = new Stopwatch();
        running.Start();
        sim.Run();
        running.Stop();
        Console.WriteLine($"Running time: {running.ElapsedMilliseconds:n} ms");
        Console.WriteLine($"Events/sec: {(sim.NrEventsProcessed / running.ElapsedMilliseconds * 1000):n}");
        Console.WriteLine($"Time per event: {(running.Elapsed.TotalSeconds / sim.NrEventsProcessed * 1_000_000_000):n} ns");
    }
}