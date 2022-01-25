using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpikingDSE;

public class MemReporter
{
    private SNN snn;
    private Dictionary<Layer, StreamWriter> memFiles;
    private string folderPath;

    public MemReporter(SNN snn, string folderPath)
    {
        this.snn = snn;
        this.folderPath = folderPath;
        this.memFiles = new();
    }

    public void RegisterLayer(Layer layer)
    {
        bool[] layerSpikes = new bool[layer.Size];
        StreamWriter sw = new StreamWriter(folderPath + "/mem_" + layer.Name + ".csv");
        sw.WriteLine("," + string.Join(",", Enumerable.Range(0, layer.Size)));
        memFiles[layer] = sw;
    }

    public void RegisterSNN(SNN snn)
    {
        foreach (var layer in snn.GetAllLayers())
            RegisterLayer(layer);
    }

    public void AdvanceLayer(Layer layer, int ts, float[] pots)
    {
        var file = memFiles[layer];
        file.WriteLine($"{ts},{string.Join(",", pots)}");
        file.Flush();
        ts++;

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