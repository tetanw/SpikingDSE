using System.Collections.Generic;

namespace SpikingDSE;

public abstract class Actor
{
    public string Name { get; protected set; }

    public abstract IEnumerable<Event> Run(Simulator env);
}
