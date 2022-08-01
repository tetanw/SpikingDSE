using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpikingDSE;

namespace SpikingDSE;

public class FileReporter
{
    private readonly StreamWriter sw;

    public FileReporter(string filePath)
    {
        sw = new StreamWriter(filePath);
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