using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SpikingDSE;

public class MapEntry
{
    public MapLayer Layer;
    public MapCore Core;
    public bool Partial;
    public int Index;
    public int Start;
    public int End;
}

public class Mapping
{
    public List<MapEntry> Mapped = new();
    public List<MapLayer> Unmapped = new();

    public void PrintReport()
    {
        Console.WriteLine("Mappings:");
        foreach (var entry in Mapped)
        {
            if (entry.Partial)
            {
                Console.WriteLine($"  {entry.Layer.Name} from {entry.Start} to {entry.End} -> {entry.Core.Name}");
            }
            else
            {
                Console.WriteLine($"  {entry.Layer.Name} -> {entry.Core.Name}");
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
        return Mapped.FindAll((m) => m.Layer.Name == name);
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
                    Layer = layer,
                    Core = core,
                    Partial = false,
                    Start = -1,
                    End = -1,
                    Index = -1
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
                    Layer = layer,
                    Core = c,
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
    public object Value;
    public string Name;
    public int NrNeurons;
    public bool Splittable;
    public object Type;

    public void Build()
    {

    }
}