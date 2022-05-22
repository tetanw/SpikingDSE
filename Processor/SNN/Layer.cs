using System;
using System.Linq;
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

public class OpCounter
{
    private Dictionary<string, int> stats = new Dictionary<string, int>();

    public void AddCount(string op, int amount)
    {
        stats.AddCount(op, amount);
    }

    public IEnumerable<(string name, int amount)> AllCounts()
    {
        return stats.Select((kv) => (kv.Key, kv.Value));
    }

    public static OpCounter Merge(IEnumerable<OpCounter> counters)
    {
        var masterCounter = new OpCounter();
        foreach (var counter in counters)
        {
            foreach (var (name, count) in counter.AllCounts())
            {
                masterCounter.AddCount(name, count);
            }
        }
        return masterCounter;
    }
}

public abstract class HiddenLayer : Layer
{
    public int TS { get; set; } = 0;
    public event Action SyncStarted;
    public event Action SyncFinished;

    public OpCounter Ops = new();
    public abstract float[] Readout();
    public abstract void Forward(int neuron);
    public abstract void Feedback(int neuron);
    public abstract bool Sync(int neuron);
    public virtual void StartSync()
    {
        SyncStarted?.Invoke();
    }
    public virtual void FinishSync()
    {
        SyncFinished?.Invoke();
    }
    public abstract int Offset();
}