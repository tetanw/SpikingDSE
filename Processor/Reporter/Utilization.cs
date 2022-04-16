using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public record struct BusyPeriod(long Busy, long Period);

public class UtilManager
{
    public Dictionary<string, BusyPeriod> busyPeriods = new();

    public void WriteBusyPeriod(string name, long busy, long period)
    {
        bool found = busyPeriods.TryGetValue(name, out BusyPeriod bp);
        if (found)
        {
            busyPeriods[name] = new BusyPeriod(bp.Busy + busy, bp.Period + period);
        }
        else
        {
            busyPeriods[name] = new BusyPeriod(busy, period);
        }
    }

    public IEnumerable<(string, BusyPeriod)> GetPeriods()
    {
        return busyPeriods.Select(kv => (kv.Key, kv.Value));
    }
}