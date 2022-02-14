using System.Collections.Generic;

namespace SpikingDSE;

public sealed class Mutex
{
    public List<(ResReqEvent, Process)> Waiting = new();
    public int Amount;

    public Mutex(int amount)
    {
        this.Amount = amount;
    }
}