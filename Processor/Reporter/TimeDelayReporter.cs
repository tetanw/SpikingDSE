using System.IO;

namespace SpikingDSE;

public class TimeDelayReporter
{
    public readonly string FilePath;
    private StreamWriter sw;

    public TimeDelayReporter(string filePath)
    {
        this.sw = new StreamWriter(filePath);
        this.FilePath = filePath;
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