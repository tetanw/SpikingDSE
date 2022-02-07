namespace SpikingDSE;

public abstract record CoreEvent();
public sealed record SyncEvent(int TS) : CoreEvent;
public sealed record SpikeEvent(Layer layer, int neuron, bool feedback, int TS) : CoreEvent;