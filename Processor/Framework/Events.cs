using System.Collections.Generic;

namespace SpikingDSE;
public class Event
{

}

public class SleepEvent : Event
{
    // To scheduler
    public long Time;
}

public class SendEvent : Event
{
    // To scheduler
    public OutPort Port;
    public object Message;

    // Result
    public long Time;
}

public class ReceiveEvent : Event
{
    // To scheduler
    public InPort Port;
    public long Time;

    // Result
    public object Message;
}

public class SelectEvent : Event
{
    // To scheduler
    public InPort[] Ports;
    public long Time;

    // Result
    public InPort Port;
    public object Message;
}

public class ResReqEvent : Event
{
    // To scheduler
    public Resource Resource;
    public int Amount;
}

public class ProcessWaitEvent : Event
{
    // To scheduler
    public Process Process;

    // Result
    public object Value;
}

public class SignalWaitEvent : Event
{
    public Signal Signal;
}
