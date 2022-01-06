using System;
using System.Collections.Generic;

namespace SpikingDSE;

public sealed class NI1 : Actor
{
    public InPort inLocal = new InPort();
    public OutPort outLocal = new OutPort();
    public InPort inRouter = new InPort();
    public OutPort outRouter = new OutPort();

    private MeshCoord thisCoord;
    public MeshCoord DestCoord;

    public NI1(MeshCoord thisCoord, MeshCoord controllerCoord)
    {
        this.thisCoord = thisCoord;
        this.DestCoord = controllerCoord;
    }

    public override IEnumerable<Event> Run(Environment env)
    {
        env.Process(RouterToLocal(env));
        env.Process(LocalToRouter(env));

        yield break;
    }

    private IEnumerable<Event> RouterToLocal(Environment env)
    {
        while (true)
        {
            var receive = env.Receive(inRouter);
            yield return receive;
            var message = ((MeshFlit)receive.Message).Message;
            yield return env.Send(outLocal, message);
        }
    }

    private IEnumerable<Event> LocalToRouter(Environment env)
    {
        while (true)
        {
            var receive = env.Receive(inLocal);
            yield return receive;
            var flit = new MeshFlit
            {
                Src = thisCoord,
                Dest = DestCoord,
                Message = receive.Message
            };
            yield return env.Send(outRouter, flit);
        }
    }
}