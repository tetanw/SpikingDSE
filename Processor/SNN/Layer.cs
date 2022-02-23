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
    public InputLayer(int size, string name = null)
    {
        this.Name = name;
        this.InputSize = -1;
        this.Size = size;
    }

    public InputLayer Copy()
    {
        return new InputLayer(this.Size, name: this.Name);
    }

    public override string ToString()
    {
        return $"Input - \"{this.Name}\"";
    }
}

public enum ResetMode
{
    Zero,
    Subtract
}

public abstract class OdinHiddenLayer : Layer
{
    public abstract void Leak();

    public abstract IEnumerable<int> Threshold();

    public abstract void ApplyThreshold(int neuron);

    public abstract void Integrate(int neuron);
}

public abstract class HiddenLayer : Layer
{
    public abstract void Forward(int neuron);
    public abstract IEnumerable<int> Sync();
}