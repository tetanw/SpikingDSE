using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public class MappedLayer
{
    public string Layer { get; set; }
    public string Core { get; set; }
    public bool Partial { get; set; }
    public int Index { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
}

public class Mapping
{
    public List<MappedLayer> Mapped { get; set; } = new();
    public List<string> Unmapped { get; set; } = new();

    public void PrintReport()
    {
        Console.WriteLine("Mappings:");
        foreach (var entry in Mapped)
        {
            if (entry.Partial)
            {
                Console.WriteLine($"  {entry.Layer} from {entry.Start} to {entry.End} -> {entry.Core}");
            }
            else
            {
                Console.WriteLine($"  {entry.Layer} -> {entry.Core}");
            }
        }
        Console.WriteLine("Unmapped:");
        foreach (var layerName in Unmapped)
        {
            Console.WriteLine($"  {layerName}");
        }
    }

    public IEnumerable<MappedLayer> GetAllSplits(string name)
    {
        return Mapped.FindAll((m) => m.Layer == name);
    }

    public void Save(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(Path.GetDirectoryName(path));

        using var fileStream = File.Create(path);
        JsonSerializer.Serialize(fileStream, this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static Mapping Load(string path)
    {
        using var fileStream = File.Open(path, FileMode.Open);
        return JsonSerializer.Deserialize<Mapping>(fileStream);
    }
}

public class CoreData
{
    public CoreSpec Spec { get; set; }
    public int NrNeurons { get; set; }
    public int NrSynapses { get; set; }
    public int NrLayers { get; set; }
    public int NrFanIn { get; set; }

    public CoreData(CoreSpec spec)
    {
        Spec = spec;
        NrNeurons = 0;
        NrSynapses = 0;
        NrLayers = 0;
        NrFanIn = 0;
    }

    public bool FitsLayer(Layer layer)
    {
        return MaximumCut(layer, layer.Size) == layer.Size;
    }

    public int MaximumCut(Layer layer, int neuronsToMap)
    {
        // Not a valid to core to use as target
        if (!Spec.AcceptedTypes.Contains(layer.TypeName))
            return 0;

        // If the maximum amount of layers is reached then also continue
        if (NrLayers == Spec.MaxLayers)
            return 0;

        // FanIn includes the reccurrent connections
        if (NrFanIn + layer.InputSize + (layer.Recurrent ? layer.Size : 0) > Spec.MaxFanIn)
            return 0;

        // What is the maximum amount of neurons that can be mapped 
        // according to neuron memory? 
        int limitedByNeuron = Spec.MaxNeurons - NrNeurons;

        // What is the maximum amount of neurons that can be mapped
        // according to synapse memory
        int freeSynapses = Spec.MaxSynapses - NrSynapses;
        int limitedBySynapse;
        if (layer.Recurrent)
            limitedBySynapse = freeSynapses / (layer.InputSize + layer.Size); // should be rounded down which int already does luckily
        else
            limitedBySynapse = freeSynapses / layer.InputSize;

        int maxCut = Math.Min(Math.Min(limitedByNeuron, limitedBySynapse), neuronsToMap);
        return maxCut;
    }

    public void AddLayer(Layer layer, int sliceSize)
    {
        NrNeurons += sliceSize;
        NrSynapses += layer.InputSize * sliceSize;
        if (layer.Recurrent)
            NrSynapses += layer.Size * sliceSize;
        NrFanIn += layer.InputSize;
        NrLayers++;
    }
}

public abstract class Mapper
{
    protected readonly HWSpec hw;
    protected readonly SNN snn;

    public Mapper(HWSpec hw, SNN snn)
    {
        this.hw = hw;
        this.snn = snn;
    }

    public abstract Mapping Run();
}