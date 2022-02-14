using System.Collections.Generic;

namespace NewSimulator;

public abstract class Actor
{
    public string Name { get; protected set; } = "";

    public abstract IEnumerable<Event?> Run(Simulator env);
}
