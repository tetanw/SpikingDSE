using NewSimulator;

public abstract class Event
{
    public Simulator Sim { get; private set; }
    public Process Process { get; private set; }

    public Event(Simulator sim)
    {
        this.Sim = sim;
        this.Process = sim.ActiveProcess!;
    }

    public virtual void Yielded() { }
}

public class Delay : Event
{
    public long Time;

    public Delay(Simulator sim, long time) : base(sim)
    {
        this.Time = time;
    }

    public override void Yielded()
    {
        Sim.ScheduleD(Process, Time);
    }
}