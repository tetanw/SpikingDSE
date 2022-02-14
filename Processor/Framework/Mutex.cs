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

    public void Decrease(int dec)
    {
        Amount -= dec;
    }

    public MutexReqEvent Wait(int amount)
    {
        return new MutexReqEvent { Mutex = this, Amount = amount };
    }
}