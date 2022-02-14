using System.Collections.Generic;

namespace SpikingDSE;

public class MutexReqEvent : Event
{
    // To scheduler
    public Mutex Mutex;
    public int Amount;
}

public sealed class Mutex
{
    public List<(MutexReqEvent, Process)> Waiting = new();
    public int Amount;

    public Mutex(int amount)
    {
        this.Amount = amount;
    }
}