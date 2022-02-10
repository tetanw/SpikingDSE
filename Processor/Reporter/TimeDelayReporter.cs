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
    }

    public void ReportDelay(long start, long end)
    {
        sw.WriteLine($"{start},{end}");
    }

    public void Finish()
    {
        sw.Close();
    }
}