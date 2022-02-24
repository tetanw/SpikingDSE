using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public class MapEntry
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
    public List<MapEntry> Mapped { get; set; } = new();
    public List<MapLayer> Unmapped { get; set; } = new();

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
        foreach (var layer in Unmapped)
        {
            Console.WriteLine($"  {layer.Name}");
        }
    }

    public IEnumerable<MapEntry> GetAllSplits(string name)
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

public interface Mapper
{
    public Mapping Run();
    public void AddCore(MapCore core);
    public void AddLayer(MapLayer layer);
}

public class FirstFitMapper : Mapper
{
    private List<MapCore> cores = new();
    private List<MapLayer> layers = new();


    public void AddCore(MapCore core)
    {
        // Adding a core that a layer can be mapped to
        cores.Add(core);
    }

    public void AddLayer(MapLayer layer)
    {
        // Add a SNN layer that needs to be mapped
        layers.Add(layer);
    }

    public Mapping Run()
    {
        var mapping = new Mapping();

        // Do the actual mapping
        Queue<MapLayer> ready = new(layers);
        while (ready.Count > 0)
        {
            var layer = ready.Dequeue();

            var core = cores.Find(c => c.NrNeurons + layer.NrNeurons <= c.MaxNrNeurons && c.AcceptedTypes.Contains(layer.Type));
            if (core != null)
            {
                core.NrNeurons += layer.NrNeurons;
                mapping.Mapped.Add(new MapEntry
                {
                    Layer = layer.Name,
                    Core = core.Name,
                    Partial = false,
                    Start = 0,
                    End = layer.NrNeurons,
                    Index = 0
                });
                continue;
            }

            // if the layer is not splittable then we can simply not map
            // the layer to a core
            if (!layer.Splittable)
            {
                mapping.Unmapped.Add(layer);
                continue;
            }

            List<MapCore> splitSet = new();
            int neuronsToMap = layer.NrNeurons;
            foreach (var c in cores)
            {
                if (!c.AcceptedTypes.Contains(layer.Type))
                    continue;

                int freeNeurons = c.MaxNrNeurons - c.NrNeurons;
                if (freeNeurons == 0)
                    continue;

                splitSet.Add(c);
                neuronsToMap -= freeNeurons;
                if (neuronsToMap <= 0)
                {
                    break;
                }
            }
            if (neuronsToMap > 0)
            {
                // the layer is too big to be mapped even though it is splittable
                mapping.Unmapped.Add(layer);
                continue;
            }
            Debug.Assert(splitSet.Count > 0);

            // actually assign the splits to each core
            int neuronsMapped = 0;
            neuronsToMap = layer.NrNeurons;
            int i = 1;
            foreach (var c in splitSet)
            {
                int freeNeurons = c.MaxNrNeurons - c.NrNeurons;
                int sliceSize = Math.Min(freeNeurons, neuronsToMap);
                mapping.Mapped.Add(new MapEntry
                {
                    Layer = layer.Name,
                    Core = c.Name,
                    Partial = true,
                    Start = neuronsMapped,
                    End = neuronsMapped + sliceSize,
                    Index = i++
                });
                c.NrNeurons += sliceSize;
                neuronsMapped += sliceSize;
                neuronsToMap -= sliceSize;
            }
            Debug.Assert(neuronsMapped == layer.NrNeurons);
        }
        return mapping;
    }
}

public class MapCore
{
    // Params
    public object Value;
    public string Name;
    public int MaxNrNeurons;
    public List<object> AcceptedTypes;

    // Variables
    public int NrNeurons = 0;

    public void Build()
    {

    }
}

public class MapLayer
{
    public object Value { get; set; }
    public string Name { get; set; }
    public int NrNeurons { get; set; }
    public bool Splittable { get; set; }
    public object Type { get; set; }

    public void Build()
    {

    }
}