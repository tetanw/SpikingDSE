using System.Collections.Generic;

namespace SpikingDSE;

public class MutexReqEvent : Event
{
    // To scheduler
    public Mutex Mutex;
    public Process Process;
    public int Amount;
}

public sealed class Mutex
{
    private Simulator env;
    public List<MutexReqEvent> Waiting = new();
    public int Amount;

    public Mutex(Simulator env, int amount)
    {
        this.env = env;
        this.Amount = amount;
    }

    public void Decrease(int dec)
    {
        Amount -= dec;
    }

    public MutexReqEvent Wait(int amount)
    {
        return new MutexReqEvent { Mutex = this, Amount = amount, Process = env.CurrentProcess };
    }
}