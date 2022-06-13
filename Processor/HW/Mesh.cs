using System;
using System.Collections.Generic;

namespace SpikingDSE;

public record struct MeshCoord(int X, int Y);

public sealed class Packet
{
    public object Src { get; set; }
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

public class MeshComm : Comm
{
    private static void ConnectRouters(Simulator sim, MeshRouter[,] routers)
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

    private static bool InMesh(int width, int height, MeshCoord coord)
    {
        var (x, y) = coord;
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private MeshRouter[,] routers;
    private int width, height;

    public MeshComm(Simulator env, int width, int height, List<Core> cores, Func<int, int, MeshRouter> constructRouter)
    {
        routers = new MeshRouter[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                routers[x, y] = constructRouter(x, y);
                env.AddActor(routers[x, y]);
            }
        }
        ConnectRouters(env, routers);

        foreach (var core in cores)
        {
            var meshLoc = (MeshCoord)core.Location;
            var (x, y) = meshLoc;

            if (!InMesh(width, height, meshLoc))
                throw new Exception("Not in mesh");

            env.AddChannel(core.Output, routers[x, y].inLocal);
            env.AddChannel(routers[x, y].outLocal, core.Input);
        }

        this.width = width;
        this.height = height;
    }

    public override string[] Report(bool header)
    {
        List<string> parts = new();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                parts.AddRange(routers[x, y].Report(header));
            }
        }
        return parts.ToArray();
    }
}