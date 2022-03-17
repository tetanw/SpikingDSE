using System.IO;

namespace SpikingDSE;

public class TimeDelayReporter
{
    public readonly string FilePath;
    private readonly StreamWriter sw;

    public TimeDelayReporter(string filePath)
    {
        sw = new StreamWriter(filePath);
        FilePath = filePath;
        sw.WriteLine("start,end,layer,hops");
    }

    public void ReportDelay(long start, long end, params string[] extra)
    {
        string parts = string.Join(",", extra);
        sw.WriteLine($"{start},{end},{parts}");
    }

    public void Finish()
    {
        sw.Close();
    }
}