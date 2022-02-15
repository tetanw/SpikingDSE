using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class CondVar<T>
{
    public T Value { get; set; }
    private Signal onChange;

    public CondVar(Simulator env, T initial)
    {
        onChange = new Signal(env);
        this.Value = initial;
    }

    public IEnumerable<Event> BlockUntil(Predicate<T> when)
    {
        bool condMet = when(Value);
        while (!condMet)
        {
            yield return onChange.Wait();
            condMet = when(Value);
        }
    }

    public void Update()
    {
        onChange.Notify();
    }
}