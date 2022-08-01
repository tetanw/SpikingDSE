using System.Collections.Generic;

namespace SpikingDSE;

public sealed class Fork : Actor
{
    public delegate void MessageSent(OutPort @out, object message);
    public MessageSent OnMessageSent;

    public InPort input = new();
    public OutPort out1 = new();
    public OutPort out2 = new();
    public OutPort out3 = new();

    public override IEnumerable<Event> Run(Simulator env)
    {
        while (true)
        {
            var recv = env.Receive(input);
            yield return recv;
            var message = recv.Message;

            yield return env.Send(out1, message);
            OnMessageSent?.Invoke(out1, message);
            yield return env.Send(out2, message);
            OnMessageSent?.Invoke(out2, message);
            yield return env.Send(out3, message);
            OnMessageSent?.Invoke(out3, message);
        }
    }
}

