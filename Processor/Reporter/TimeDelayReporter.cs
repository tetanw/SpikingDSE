using System.IO;

namespace SpikingDSE;

public class TimeDelayReporter
{
    private string filePath;
    private StreamWriter sw;

    public TimeDelayReporter(string filePath)
    {
        this.sw = new StreamWriter(filePath);
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