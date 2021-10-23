using System.Collections.Generic;

namespace SpikingDSE
{
    public class MeshFlit
    {
        public int DX;
        public int DY;
        public object Message;
    }

    public interface Locator<T>
    {
        public T Locate(int packetID);
    }

    public class MeshLocator : Locator<(int x, int y)>
    {
        private int width, height;

        public MeshLocator(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public (int x, int y) Locate(int packetID)
        {
            int x = packetID % width;
            int y = packetID / width;
            return (x, y);
        }
    }

    public class XYRouter : Actor
    {
        public InPort inNorth;
        public InPort inSouth;
        public InPort inEast;
        public InPort inWest;
        public InPort fromCore;
        public OutPort outNorth;
        public OutPort outSouth;
        public OutPort outEast;
        public OutPort outWest;
        public OutPort toCore;

        public long processingDelay;

        public XYRouter(long processingDelay, string name = "")
        {
            this.Name = name;
            this.processingDelay = processingDelay;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                // 1. Monitor the inputs for any packet
                var select = env.Select(inNorth, inEast, inSouth, inWest, fromCore);
                yield return select;
                MeshFlit flit = (MeshFlit)select.Message;

                // 2. Add a delay to simulate processing
                yield return env.Delay(processingDelay);

                // 3. Determine into which output port it goes
                OutPort outPort;
                if (flit.DX > 0)
                {
                    // East
                    flit.DX--;
                    outPort = outEast;
                }
                else if (flit.DX < 0)
                {
                    // West
                    flit.DX++;
                    outPort = outWest;
                }
                else if (flit.DY > 0)
                {
                    // North
                    flit.DY--;
                    outPort = outNorth;
                }
                else if (flit.DY < 0)
                {
                    // South
                    flit.DY++;
                    outPort = outSouth;
                }
                else
                {
                    // Chip
                    outPort = toCore;
                }

                // 4. Send to right port
                if (outPort == null)
                {
                    throw new System.Exception("Outport was not configured");
                }
                yield return env.Send(outPort, flit);
            }
        }
    }

    public class MeshNI : Actor
    {
        public InPort FromMesh;
        public InPort FromCore;
        public OutPort ToMesh;
        public OutPort ToCore;

        private Locator<(int x, int y)> locator;
        public int srcX, srcY;

        public MeshNI(int x, int y, Locator<(int x, int y)> locator, string name = "")
        {
            this.srcX = x;
            this.srcY = y;
            this.locator = locator;
            this.Name = name;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                var select = env.Select(FromMesh, FromCore);
                yield return select;

                if (select.Port == FromMesh)
                {
                    var packet = (MeshFlit)select.Message;
                    yield return env.Send(ToCore, packet.Message);
                }
                else if (select.Port == FromCore)
                {
                    var packet = (Packet)select.Message;
                    var (destX, destY) = locator.Locate(packet.ID);
                    var flit = new MeshFlit
                    {
                        DX = destX - srcX,
                        DY = destY - srcY,
                        Message = packet.Message
                    };
                    yield return env.Send(ToMesh, flit);
                }
            }
        }
    }
}