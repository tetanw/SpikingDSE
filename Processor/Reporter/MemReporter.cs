using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpikingDSE;

namespace SpikingDSE;

public class MemReporter
{
    private readonly Dictionary<Layer, StreamWriter> memFiles;
    private readonly string folderPath;

    public MemReporter(string folderPath)
    {
        this.folderPath = folderPath;
        memFiles = new();
    }

    public void RegisterLayer(Layer layer)
    {
        var sw = new StreamWriter(folderPath + "/mem_" + layer.Name + ".csv");
        sw.WriteLine("," + string.Join(",", Enumerable.Range(0, layer.Size)));
        memFiles[layer] = sw;
    }

    public void RegisterSNN(SNN snn)
    {
        foreach (var layer in snn.GetAllLayers().Where(l => l is HiddenLayer))
            RegisterLayer(layer);
    }

    public void AdvanceLayer(HiddenLayer layer)
    {
        var file = memFiles[layer];
        file.WriteLine($"{layer.TS},{string.Join(",", layer.Readout())}");
        file.Flush();
    }

    public void Finish()
    {
        foreach (var file in memFiles.Values)
        {
            file.Close();
        }
    }

    public int NrSpikes { get; private set; }
}