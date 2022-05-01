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
        using var fileStream = File.Create(path);
        JsonSerializer.Serialize(fileStream, this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static Mapping Load(string path)
    {
        using var fileStream = File.Open(path, FileMode.Open);
        return JsonSerializer.Deserialize<Mapping>(fileStream);
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

public class FirstFitMapper : Mapper
{
    record class CoreData
    {
        public CoreSpec Spec { get; set; }
        public int NrNeurons { get; set; }
        public int NrSynapses { get; set; }
        public int NrLayers { get; set; }

        public bool FitsLayer(Layer layer)
        {
            return NrNeurons + layer.Size <= Spec.MaxNeurons &&
                            NrSynapses + layer.InputSize * layer.Size <= Spec.MaxSynapses &&
                            NrLayers < Spec.MaxLayers &&
                            Spec.AcceptedTypes.Contains(layer.TypeName);
        }
    }

    public FirstFitMapper(HWSpec hw, SNN snn) : base(hw, snn)
    {
    }

    public override Mapping Run()
    {
        var mapping = new Mapping();
        var layers = snn.GetAllLayers();

        // Do the actual mapping
        var unmapped = new Queue<Layer>(layers);
        var sortedCores = hw.Cores.Select(c => new CoreData { Spec = c, NrNeurons = 0, NrSynapses = 0 }).ToList();
        sortedCores.Sort((c1, c2) => c1.Spec.Priority < c2.Spec.Priority ? 1 : -1);
        while (unmapped.Count > 0)
        {
            var layer = unmapped.Dequeue();

            var core = sortedCores.Find(c => c.FitsLayer(layer));
            if (core != null)
            {
                core.NrNeurons += layer.Size;
                core.NrSynapses += layer.InputSize * layer.Size;
                core.NrLayers++;
                mapping.Mapped.Add(new MappedLayer
                {
                    Layer = layer.Name,
                    Core = core.Spec.Name,
                    Partial = false,
                    Start = 0,
                    End = layer.Size,
                    Index = 0
                });
                continue;
            }

            // if the layer is not splittable then we can simply not map
            // the layer to a core
            if (!layer.Splittable)
            {
                mapping.Unmapped.Add(layer.Name);
                continue;
            }

            Dictionary<CoreData, MappedLayer> splitSet = new();
            int i = 1;
            int mappedNeurons = 0;
            foreach (var c in sortedCores)
            {
                // Not a valid to core to use as target
                if (!c.Spec.AcceptedTypes.Contains(layer.TypeName))
                    continue;

                // If the maximum amount of layers is reached then also continue
                if (c.NrLayers == c.Spec.MaxLayers)
                    continue;

                // What is the maximum amount of neurons that can be mapped 
                // according to neuron memory? 
                int limitedByNeuron = c.Spec.MaxNeurons - c.NrNeurons;

                // What is the maximum amount of neurons that can be mapped
                // according to synapse memory
                int freeSynapses = c.Spec.MaxSynapses - c.NrSynapses;
                int limitedBySynapse = freeSynapses / layer.InputSize;

                int neuronsToMap = layer.Size - mappedNeurons;
                int toMap = Math.Min(Math.Min(limitedByNeuron, limitedBySynapse), neuronsToMap);
                if (toMap == 0)
                    continue;

                splitSet.Add(c, new MappedLayer
                {
                    Layer = layer.Name,
                    Core = c.Spec.Name,
                    Partial = true,
                    Start = mappedNeurons,
                    End = mappedNeurons + toMap,
                    Index = i++
                });
                mappedNeurons += toMap;
                if (mappedNeurons == layer.Size)
                {
                    break;
                }
            }
            if (mappedNeurons != layer.Size)
            {
                // the layer is too big to be mapped even though it is splittable
                mapping.Unmapped.Add(layer.Name);
                continue;
            }
            Debug.Assert(splitSet.Count > 0);
            Debug.Assert(mappedNeurons == layer.Size);

            // actually assign the splits to each core
            foreach (var (c, l) in splitSet)
            {
                mapping.Mapped.Add(l);
                var sliceSize = l.End - l.Start;
                c.NrNeurons += sliceSize;
                c.NrSynapses += layer.InputSize * sliceSize;
                c.NrLayers++;
            }
        }
        return mapping;
    }
}