namespace SpikingDSE;

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
