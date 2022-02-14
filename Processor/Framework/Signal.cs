using System.Collections.Generic;

namespace SpikingDSE;

public class SignalWaitEvent : Event
{
    public Process Process;
}

public sealed class Signal
{
    private Simulator env;

    private List<SignalWaitEvent> waiting = new();

    public Signal(Simulator env)
    {
        this.env = env;
    }

    public void Notify()
    {
        foreach (var waitingEv in waiting)
        {
            env.Schedule(waitingEv.Process);
        }
        waiting.Clear();
    }

    public SignalWaitEvent Wait()
    {
        var w = new SignalWaitEvent { Process = env.CurrentProcess };
        waiting.Add(w);
        return w;
    }
}
