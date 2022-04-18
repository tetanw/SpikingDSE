using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class CondVar<T>
{
    public T Value { get; set; }
    private readonly Signal onChange;

    public CondVar(Simulator env, T initial)
    {
        onChange = new Signal(env);
        Value = initial;
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


public class Condition
{
    private readonly Signal onChange;
    private readonly Func<bool> condMet;

    public Condition(Simulator env, Func<bool> condMet)
    {
        onChange = new Signal(env);
        this.condMet = condMet;
    }

    public IEnumerable<Event> Wait()
    {
        bool ready = condMet();
        while (!ready)
        {
            yield return onChange.Wait();
            ready = condMet();
        }
    }

    public bool Poll()
    {
        return condMet();
    }

    public void Update()
    {
        onChange.Notify();
    }
}