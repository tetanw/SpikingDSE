namespace SpikingDSE;

public class CoreEvent { }

public class SyncEvent : CoreEvent
{
    public int TS = -1;
    public long CreatedAt = -1;
}

public class SpikeEvent : CoreEvent
{
    public Layer Layer = null;
    public int Neuron = -1;
    public bool Feedback = false;
    public int TS = -1;
    public long CreatedAt = -1;
}