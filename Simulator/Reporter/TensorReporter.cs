using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public record SpikeFile(bool[] Spikes, StreamWriter Sw);

public class TensorReporter
{
    private readonly SNN snn;
    private readonly Dictionary<Layer, SpikeFile> spikeFiles;
    private readonly string folderPath;
    private readonly string prefix;

    public TensorReporter(SNN snn, string folderPath, string prefix = "spike")
    {
        this.snn = snn;
        this.folderPath = folderPath;
        this.spikeFiles = new();
        this.prefix = prefix;
    }

    public void RegisterLayer(Layer layer)
    {
        bool[] layerSpikes = new bool[layer.Size];
        var sw = new StreamWriter($"{folderPath}/{prefix}_{layer.Name}.csv");
        sw.WriteLine("," + string.Join(",", Enumerable.Range(0, layer.Size)));
        spikeFiles[layer] = new SpikeFile(layerSpikes, sw);
    }

    public void RegisterSNN(SNN snn)
    {
        foreach (var layer in snn.GetAllLayers())
            RegisterLayer(layer);
    }

    public void InformSpike(Layer layer, int neuron)
    {
        spikeFiles[layer].Spikes[neuron] = true;
        NrSpikes += 1;
    }

    public void AdvanceTimestep(int ts)
    {
        var allLayers = snn.GetAllLayers();
        foreach (var (layer, spikeFile) in spikeFiles)
        {
            var spikes = Enumerable.Range(0, layer.Size).Select(i => spikeFile.Spikes[i] ? "1.0" : "0.0").ToArray();
            spikeFile.Sw.WriteLine($"{ts},{string.Join(",", (string[])spikes)}");
            spikeFile.Sw.Flush();

            // Reset spikes on layer
            Array.Fill(spikeFile.Spikes, false);
        }
    }

    public void Finish()
    {
        foreach (var spikeFile in spikeFiles.Values)
        {
            spikeFile.Sw.Close();
        }
    }

    public int NrSpikes { get; private set; }
}