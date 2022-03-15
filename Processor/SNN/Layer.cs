using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class Layer
{
    public int InputSize { get; protected set; }
    public int Size { get; protected set; }
    public string Name { get; protected set; }

    public virtual Layer Slice(int start, int end, int partNr)
    {
        throw new NotImplementedException("Slice is not implemented by default");
    }

    public virtual Layer Copy()
    {
        throw new NotImplementedException("Copy is not implemented by default");
    }
}

public class InputLayer : Layer
{
    public InputLayer(int size, string name = null)
    {
        this.Name = name;
        this.InputSize = -1;
        this.Size = size;
    }

    public override Layer Copy()
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
    public abstract bool IsRecurrent();
    public abstract int Offset();
}