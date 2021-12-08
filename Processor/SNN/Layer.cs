using System.Collections.Generic;

namespace SpikingDSE;

public class Layer
{
    public int InputSize { get; protected set; }
    public int Size { get; protected set; }
    public string Name { get; protected set; }
}

public class InputLayer : Layer
{
    public readonly ISpikeSource spikeSource;

    public InputLayer(ISpikeSource spikeSource, string name = null)
    {
        this.spikeSource = spikeSource;
        this.Name = name;
        this.InputSize = -1;
        this.Size = spikeSource.NrNeurons();
    }
}

public enum ResetMode
{
    Zero,
    Subtract
}

public abstract class HiddenLayer : Layer
{
    public abstract void Leak();

    public abstract IEnumerable<int> Threshold();

    public abstract void ApplyThreshold(int neuron);

    public abstract void Integrate(int neuron);
}