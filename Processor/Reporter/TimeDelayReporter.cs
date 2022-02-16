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
        sw.WriteLine("start,end,layer");
    }

    public void ReportDelay(long start, long end, string extra)
    {
        sw.WriteLine($"{start},{end},{extra}");
    }

    public void Finish()
    {
        sw.Close();
    }
}