using System.Collections.Generic;

namespace SpikingDSE;

public struct Receival<T>
{
    public InPort Port;
    public int PortNr;
    public T Message;
}

public class Any
{
    public static Buffer<Receival<T>> AnyOf<T>(Simulator env, params InPort[] inPorts)
    {
        var buffer = new Buffer<Receival<T>>(env, 1);

        for (int portNr = 0; portNr < inPorts.Length; portNr++)
        {
            var port = inPorts[portNr];
            if (port.IsBound)
                env.Process(Receiver(env, buffer, port, portNr));
        }

        return buffer;
    }

    private static IEnumerable<Event> Receiver<T>(Simulator env, Buffer<Receival<T>> buffer, InPort port, int portNr)
    {
        while (true)
        {
            var rcv = env.Receive(port, ack: false);
            yield return rcv;
            yield return buffer.RequestWrite();
            buffer.Write(new Receival<T> { Message = (T)rcv.Message, Port = port, PortNr = portNr });
            buffer.ReleaseWrite();
            env.RcvAck(port);
        }
    }
}