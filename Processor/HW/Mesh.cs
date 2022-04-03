using System;
using System.Collections.Generic;

namespace SpikingDSE;

public record struct MeshCoord(int X, int Y);

public sealed class Packet
{
    public object Src { get; set;}
    public object Dest { get; set; }
    public object Message { get; set; }
    public int NrHops { get; set; } = 0;
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

    public static MergeSplit ConnectMergeSplit(Simulator sim, MeshRouter[,] routers)
    {
        int width = routers.GetLength(0);
        int height = routers.GetLength(1);
        var mergeSplit = new MergeSplit(width * 2 + height * 2, "MergeSplit");
        sim.AddActor(mergeSplit);
        int i = 0;
        // FIXME: In some situations to many inputs are created
        for (int y = 0; y < height; y++)
        {
            sim.AddChannel(mergeSplit.FromMesh[i++], routers[0, y].outWest);

            if (width - 1 > 0)
                sim.AddChannel(mergeSplit.FromMesh[i++], routers[width - 1, y].outEast);
        }

        for (int x = 0; x < width; x++)
        {
            sim.AddChannel(mergeSplit.FromMesh[i++], routers[x, 0].outSouth);

            if (height - 1 > 0)
                sim.AddChannel(mergeSplit.FromMesh[i++], routers[x, height - 1].outNorth);
        }
        return mergeSplit;
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

    public static bool InMesh(int width, int height, MeshCoord coord)
    {
        var (x, y) = coord;
        return x >= 0 && x < width && y >= 0 && y < height;
    }
}