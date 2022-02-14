
using NewSimulator;

public class WaitProcess : Event
{
    public Process Waiting;

    public WaitProcess(Simulator sim, Process waiting) : base(sim)
    {
        this.Waiting = waiting;
    }

    public override void Yielded()
    {
        Waiting.OnComplete += ProcessCompleted;
    }

    private void ProcessCompleted()
    {
        Sim.Schedule(Process);
    }
}

public class Process : IComparable<Process>
{
    public IEnumerator<Event?> Runnable;
    public long Time;
    public Action? OnComplete;

    public Process(IEnumerator<Event?> runnable, long time)
    {
        this.Runnable = runnable;
        this.Time = time;
    }

    public int CompareTo(Process? other)
    {
        return this.Time.CompareTo(other?.Time);
    }
}