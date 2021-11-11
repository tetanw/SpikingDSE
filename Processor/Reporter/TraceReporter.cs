using System.IO;

public class TraceReporter
{
    private int TS;
    private StreamWriter sw;

    public TraceReporter(string reportPath)
    {
        this.sw = new StreamWriter(reportPath);
    }

    public void InputSpike(int neuron, long time)
    {
        sw.WriteLine($"1,{neuron},{time},{TS}");
    }

    public void OutputSpike(int neuron, long time)
    {
        sw.WriteLine($"0,{neuron},{time},{TS}");
    }

    public void TimeRef(long time)
    {
        sw.WriteLine($"2,{time}");
    }

    public void AdvanceTimestep(int ts)
    {
        this.TS = ts;
    }

    public void Finish()
    {
        sw.Flush();
        sw.Close();
    }
}