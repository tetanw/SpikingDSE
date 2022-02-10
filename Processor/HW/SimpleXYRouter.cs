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

    public override IEnumerable<Event> Run(Environment env)
    {
        while (true)
        {
            // 1. Monitor the inputs for any packet
            var select = env.Select(inNorth, inEast, inSouth, inWest, inLocal);
            yield return select;
            MeshPacket flit = (MeshPacket)select.Message;

            // 2. Add a delay to simulate processing
            yield return env.Delay(processingDelay);

            // 3. Determine into which output port it goes
            int DX = flit.Dest.x - x;
            int DY = flit.Dest.y - y;
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
            yield return env.Send(outPort, flit);
        }
    }
}