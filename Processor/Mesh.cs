using System.Collections.Generic;

namespace SpikingDSE
{
    public class MeshFlit
    {
        public int DestX;
        public int DestY;
        public object Message;

        public override string ToString()
        {
            return $"MeshFlit {{ DestX: {DestX}, DestY: {DestY}, Message: {Message} }}";
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
        private int x, y;

        public XYRouter(int x, int y, long processingDelay, string name = "")
        {
            this.Name = name;
            this.x = x;
            this.y = y;
            this.processingDelay = processingDelay;
        }

        public override IEnumerable<Command> Run()
        {
            // TODO: Seperate receiving and sending threads else, DEADLOCK!
            while (true)
            {
                // 1. Monitor the inputs for any packet
                var select = env.Select(inNorth, inEast, inSouth, inWest, fromCore);
                yield return select;
                MeshFlit flit = (MeshFlit)select.Message;

                // 2. Add a delay to simulate processing
                yield return env.Delay(processingDelay);

                // 3. Determine into which output port it goes
                int DX = flit.DestX - x;
                int DY = flit.DestY - y;
                OutPort outPort;
                if (DX > 0)
                {
                    // East
                    outPort = outEast;
                }
                else if (DX < 0)
                {
                    // West
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
                    outPort = toCore;
                }

                // 4. Send to right port
                if (!outPort.IsBound)
                {
                    throw new System.Exception("Outport was not configured");
                }
                yield return env.Send(outPort, flit);
            }
        }
    }

    public class MeshUtils
    {
        public delegate XYRouter ConstructRouter(int x, int y);

        public static XYRouter[,] CreateMesh(Simulator sim, int width, int height, ConstructRouter constructRouter)
        {
            XYRouter[,] routers = new XYRouter[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    routers[x, y] = sim.AddProcess(constructRouter(x, y));
                }
            }

            ConnectRouters(sim, routers);
            return routers;
        }

        public static void ConnectRouters(Simulator sim, XYRouter[,] routers)
        {
            int width = routers.GetLength(0);
            int height = routers.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // wire up west side if possible
                    if (x > 0)
                    {
                        sim.AddChannel(ref routers[x, y].outWest, ref routers[x - 1, y].inEast);
                    }

                    // wire up east side if possible
                    if (x < width - 1)
                    {
                        sim.AddChannel(ref routers[x, y].outEast, ref routers[x + 1, y].inWest);
                    }

                    // wire up south side if possible
                    if (y > 0)
                    {
                        sim.AddChannel(ref routers[x, y].outSouth, ref routers[x, y - 1].inNorth);
                    }

                    // wire up north side if possible
                    if (y < height - 1)
                    {
                        sim.AddChannel(ref routers[x, y].outNorth, ref routers[x, y + 1].inSouth);
                    }
                }
            }
        }
    }
}