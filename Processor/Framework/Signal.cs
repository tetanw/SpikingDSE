using System.Collections.Generic;

namespace SpikingDSE;

public class SignalWaitEvent : Event
{
    public Signal Signal;
}

public sealed class Signal
{
    private Simulator env;

    public List<Process> Waiting = new();

    public Signal(Simulator env)
    {
        this.env = env;
    }

    public void Notify()
    {
        foreach (var process in Waiting)
        {
            env.Schedule(process);
        }
        Waiting.Clear();
    }

    public SignalWaitEvent Wait()
    {
        return new SignalWaitEvent { Signal = this };
    }
}
