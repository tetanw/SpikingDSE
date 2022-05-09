using System;
using System.Collections.Generic;

namespace SpikingDSE;

public sealed class SimpleXYRouter : MeshRouter
{
    public long processingDelay;

    public SimpleXYRouter(int x, int y, long processingDelay, string name = "")
    {
        this.Name = name;
        this.x = x;
        this.y = y;
        this.processingDelay = processingDelay;
    }

    public override double Energy(long now) => 0.0;

    public override double Memory() => 0.0;

    public override string Report(bool header) => string.Empty;

    public override IEnumerable<Event> Run(Simulator env)
    {
        var inputSelect = Any.AnyOf<Packet>(env, inNorth, inEast, inSouth, inWest, inLocal);

        while (true)
        {
            // 1. Monitor the inputs for any packet
            yield return inputSelect.RequestRead();
            var packet = inputSelect.Read().Message;
            inputSelect.ReleaseRead();

            // 2. Add a delay to simulate processing
            yield return env.Delay(processingDelay);

            // 3. Determine into which output port it goes
            var destCoord = (MeshCoord) packet.Dest;
            int DX = destCoord.X - x;
            int DY = destCoord.Y - y;
            OutPort outPort;
            if (DX > 0)
            {
                // East
                outPort = outEast;
            }
            else if (DX < 0)
            {
                outPort = outWest;
            }
            else if (DY > 0)
            {
                // North
                outPort = outNorth;
            }
            else if (DY < 0)
            {
                // South
                outPort = outSouth;
            }
            else
            {
                // Chip
                outPort = outLocal;
            }

            // 4. Send to right port
            yield return env.Send(outPort, packet);
        }
    }
}