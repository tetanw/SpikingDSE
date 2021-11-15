using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public record SpikeFile(bool[] spikes, StreamWriter sw);

public class TensorReporter
{
    private SNN snn;
    private Dictionary<Layer, SpikeFile> spikeFiles;
    private string folderPath;

    public TensorReporter(SNN snn, string folderPath)
    {
        this.snn = snn;
        this.folderPath = folderPath;
        this.spikeFiles = new();
    }

    public void RegisterLayer(Layer layer)
    {
        bool[] layerSpikes = new bool[layer.Size];
        StreamWriter sw = new StreamWriter(folderPath + "/spike_" + layer.Name + ".csv");
        sw.WriteLine("," + string.Join(",", Enumerable.Range(0, layer.Size)));
        spikeFiles[layer] = new SpikeFile(layerSpikes, sw);
    }

    public void InformSpike(Layer layer, int neuron)
    {
        spikeFiles[layer].spikes[neuron] = true;
        NrSpikes += 1;
    }

    public void AdvanceTimestep(int ts)
    {
        var allLayers = snn.GetAllLayers();
        foreach (var (layer, spikeFile) in spikeFiles)
        {
            var spikes = Enumerable.Range(0, layer.Size).Select(i => spikeFile.spikes[i] ? "1.0" : "0.0").ToArray();
            spikeFile.sw.WriteLine($"{ts},{string.Join(",", spikes)}");
            spikeFile.sw.Flush();

            // Reset spikes on layer
            Array.Fill(spikeFile.spikes, false);
        }
    }

    public void Finish()
    {
        foreach (var spikeFile in spikeFiles.Values)
        {
            spikeFile.sw.Close();
        }
    }

    public int NrSpikes { get; private set; }
}