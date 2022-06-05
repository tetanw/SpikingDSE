using System.Collections.Generic;

namespace SpikingDSE;

public abstract class Controller : Core
{
    protected ISpikeSource spikeSource;

    public void AddSource(ISpikeSource spikeSource)
    {
        this.spikeSource = spikeSource;
    }
}

public abstract class Core : Actor
{
    public object Location { get; set; }
    public OutPort Output { get; set; }
    public InPort Input { get; set; }
    public MappingManager Mapping { get; set; }

    public virtual string Report(long now, bool header) { return ""; }
}

public class CoreEvent { }

public class SyncEvent : CoreEvent
{
    public int TS = -1;
    public long CreatedAt = -1;
    public List<Layer> Layers = null;
}

public class ReadyEvent : CoreEvent
{
    public int TS = -1;
    public object Core = null;
}

public class SpikeEvent : CoreEvent
{
    public Layer Layer = null;
    public int Neuron = -1;
    public bool Feedback = false;
    public int TS = -1;
    public long CreatedAt = -1;
    public long ReceivedAt = -1;
}