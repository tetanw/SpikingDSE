namespace SpikingDSE;

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
    public long TransferTime;

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

public sealed class Channel
{
    public string Name;
    public OutPort OutPort;
    public InPort InPort;

    public Process SendProcess;
    public SendEvent SendEvent;
    public Process ReceiveProcess;
    public Event ReceiveEvent;

    public override string ToString()
    {
        return Name;
    }
}
