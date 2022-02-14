using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SpikingDSE;

public sealed class Process : IComparable<Process>
{
    public IEnumerator<Event> Runnable;
    public long Time;
    public List<Process> Waiting;

    public int CompareTo([AllowNull] Process other)
    {
        return Time.CompareTo(other.Time);
    }

    public override string ToString()
    {
        return Runnable.ToString();
    }
}