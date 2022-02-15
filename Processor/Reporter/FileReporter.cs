using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpikingDSE;

namespace SpikingDSE;

public class FileReporter
{
    private StreamWriter sw;

    public FileReporter(string filePath)
    {
        this.sw = new StreamWriter(filePath);
    }

    public void ReportLine(string line)
    {
        sw.WriteLine(line);
    }

    public void Finish()
    {
        sw.Flush();
        sw.Close();
    }
}