using System;
using System.Collections.Generic;

namespace SpikingDSE;

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

    public abstract double Energy(long now);
}

