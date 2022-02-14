namespace NewSimulator;

public sealed class SignalWait : Event
{
    public SignalWait(Simulator sim) : base(sim)
    {
    }

    public void Trigger()
    {
        Sim.Schedule(Process);
    }
}

public class Signal
{
    private Simulator env;
    private List<SignalWait> waiting = new();

    public Signal(Simulator env)
    {
        this.env = env;
    }

    public SignalWait Wait()
    {
        var w = new SignalWait(env);
        waiting.Add(w);
        return w;
    }

    public void Notify()
    {
        foreach (var w in waiting)
        {
            w.Trigger();
        }
    }
}