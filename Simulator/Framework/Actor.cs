using System.Collections.Generic;

namespace SpikingDSE;

public abstract class Actor
{
    public string Name { get; set; }
    public int NrEvents { get; set; }

    public abstract IEnumerable<Event> Run(Simulator env);
}
