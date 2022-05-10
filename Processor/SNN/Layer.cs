using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class Layer
{
    public int InputSize { get; protected set; }
    public int Size { get; protected set; }
    public string Name { get; protected set; }
    public string TypeName { get; protected set; }
    public bool Splittable { get; protected set; }
    public bool Recurrent { get; protected set; }
    public int NrSynapses { get; protected set; }

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
        Name = name;
        InputSize = 0;
        Size = size;
        TypeName = "input";
        Splittable = false;
    }

    public override Layer Copy()
    {
        return new InputLayer(Size, name: Name);
    }

    public override string ToString()
    {
        return $"Input - \"{Name}\"";
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
    public abstract float[] Readout();
    public abstract void Forward(int neuron);
    public abstract void Feedback(int neuron);
    public abstract bool Sync(int neuron);
    public virtual void StartSync() {}
    public virtual void FinishSync() {}
    public abstract int Offset();
}