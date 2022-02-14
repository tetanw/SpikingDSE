namespace NewSimulator;

public class Channel
{
    public string Name;
    public Queue<Send> SendWaiting = new();
    public Queue<Receive> ReceiveWaiting = new();

    public Channel(string name)
    {
        this.Name = name;
    }
}

public class Send : Event
{
    public Channel Channel;
    public object? Message;

    public Send(Simulator sim, Channel channel, object? message) : base(sim)
    {
        this.Channel = channel;
        this.Message = message;
    }

    public override void Yielded()
    {
        if (Channel.ReceiveWaiting.Count > 0)
        {
            var rcv = Channel.ReceiveWaiting.Dequeue();
            rcv.Value = Message;
            Sim.Schedule(rcv.Process);
            Sim.Schedule(this.Process);
        }
        else
        {
            Channel.SendWaiting.Enqueue(this);
        }
    }
}

public class Receive : Event
{
    public Channel Channel;
    public object? Value;

    public Receive(Simulator sim, Channel channel) : base(sim)
    {
        this.Channel = channel;
    }

    public override void Yielded()
    {
        if (Channel.SendWaiting.Count > 0)
        {
            var snd = Channel.SendWaiting.Dequeue();
            Value = snd.Message;
            Sim.Schedule(snd.Process);
            Sim.Schedule(this.Process);
        }
        else
        {
            Channel.ReceiveWaiting.Enqueue(this);
        }
    }
}