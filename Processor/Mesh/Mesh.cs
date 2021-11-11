using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    public struct MeshCoord
    {
        public readonly int x;
        public readonly int y;

        public MeshCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public sealed class MeshFlit
    {
        public MeshCoord Src;
        public MeshCoord Dest;
        public object Message;

        public override string ToString()
        {
            return $"MeshFlit {{ Route: ({Src.x}, {Src.y}) -> ({Dest.x}, {Dest.y}), Message: {Message} }}";
        }
    }

    public sealed class MeshDir
    {
        public const int North = 0;
        public const int East = 1;
        public const int South = 2;
        public const int West = 3;
        public const int Local = 4;
    }

    public sealed class MeshUtils
    {
        public delegate MeshRouter ConstructRouter(int x, int y);

        public static MeshRouter[,] CreateMesh(Simulator sim, int width, int height, ConstructRouter constructRouter)
        {
            MeshRouter[,] routers = new MeshRouter[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    routers[x, y] = sim.AddActor(constructRouter(x, y));
                }
            }

            ConnectRouters(sim, routers);
            return routers;
        }

        public static void ConnectRouters(Simulator sim, MeshRouter[,] routers)
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
                        sim.AddChannel(routers[x, y].outWest, routers[x - 1, y].inEast);
                    }

                    // wire up east side if possible
                    if (x < width - 1)
                    {
                        sim.AddChannel(routers[x, y].outEast, routers[x + 1, y].inWest);
                    }

                    // wire up south side if possible
                    if (y > 0)
                    {
                        sim.AddChannel(routers[x, y].outSouth, routers[x, y - 1].inNorth);
                    }

                    // wire up north side if possible
                    if (y < height - 1)
                    {
                        sim.AddChannel(routers[x, y].outNorth, routers[x, y + 1].inSouth);
                    }
                }
            }
        }
    }
}