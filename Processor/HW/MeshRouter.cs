using System;
using System.Linq;
using System.Collections.Generic;

namespace SpikingDSE;

public class MeshGrid : Comm
{
    private List<MeshRouter> routers { get; set; } = new();
    private int width, height;

    public MeshGrid(Simulator env, int width, int height, Func<int, int, MeshRouter> createRouter)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                env.AddActor(createRouter(x, y));
            }
        }

        routers = null;
        this.width = width;
        this.height = height;

    }

    public override string[] Report(bool header)
    {
        return routers.SelectMany(r => r.Report(header)).ToArray();
    }
}

public abstract class MeshRouter : Actor
{
    public int x, y;

    public InPort inNorth = new();
    public InPort inSouth = new();
    public InPort inEast = new();
    public InPort inWest = new();
    public InPort inLocal = new();
    public OutPort outNorth = new();
    public OutPort outSouth = new();
    public OutPort outEast = new();
    public OutPort outWest = new();
    public OutPort outLocal = new();

    public abstract string[] Report(bool header);
}

