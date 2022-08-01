using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SpikingDSE;

public class ProcessWaitEvent : Event
{
    // To scheduler
    public Process Process;

    // Result
    public object Value;
}

public sealed class Process : IComparable<Process>
{
    public IEnumerator<Event> Runnable;
    public long Time;
    public Actor Actor;
    public bool IsScheduled = false;
    public List<Process> Waiting = new();

    public int CompareTo([AllowNull] Process other)
    {
        return Time.CompareTo(other.Time);
    }

    public override string ToString()
    {
        return Runnable.ToString();
    }
}