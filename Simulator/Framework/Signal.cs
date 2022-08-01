using System.Collections.Generic;

namespace SpikingDSE;

public class SignalWaitEvent : Event
{
    public Signal Signal;
    public Process Process;
}

public sealed class Signal
{
    private readonly Simulator env;

    public List<SignalWaitEvent> Waiting = new();

    public Signal(Simulator env)
    {
        this.env = env;
    }

    public void Notify()
    {
        foreach (var waitingEv in Waiting)
        {
            if (!waitingEv.Process.IsScheduled)
                env.Schedule(waitingEv.Process);
        }
        Waiting.Clear();
    }

    public SignalWaitEvent Wait()
    {
        var w = new SignalWaitEvent { Signal = this, Process = env.CurrentProcess };
        return w;
    }
}
