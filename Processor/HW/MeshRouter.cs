using System;
using System.Collections.Generic;

namespace SpikingDSE;

public abstract class MeshRouter : Actor
{
    public int x, y;

    public InPort inNorth = new InPort();
    public InPort inSouth = new InPort();
    public InPort inEast = new InPort();
    public InPort inWest = new InPort();
    public InPort inLocal = new InPort();
    public OutPort outNorth = new OutPort();
    public OutPort outSouth = new OutPort();
    public OutPort outEast = new OutPort();
    public OutPort outWest = new OutPort();
    public OutPort outLocal = new OutPort();
}

