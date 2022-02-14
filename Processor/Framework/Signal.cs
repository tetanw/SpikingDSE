using System.Collections.Generic;

namespace SpikingDSE;

public class SignalWaitEvent : Event
{
    public Signal Signal;
}

public sealed class Signal
{
    public List<Process> Waiting;
}
