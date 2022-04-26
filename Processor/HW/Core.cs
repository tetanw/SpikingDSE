using System.Collections.Generic;

namespace SpikingDSE;

public interface ICore
{
    public object GetLocation();
    public string Name();
    public OutPort Output();
    public InPort Input();
    public double Energy(long now);
}

public class CoreEvent { }

public class SyncEvent : CoreEvent
{
    public int TS = -1;
    public long CreatedAt = -1;
    public List<Layer> Layers = null;
}

public class SyncDone : CoreEvent
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