using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SpikingDSE;

public class Mapper2
{
    public delegate void OnMappingFound(MapLayer layer, MapCore core, bool partial, int start, int end);
    public OnMappingFound MappingFound;

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

    public List<MapLayer> Run()
    {
        List<MapLayer> unmapped = new();

        // Do the actual mapping
        Queue<MapLayer> ready = new(layers);
        while (ready.Count > 0)
        {
            var layer = ready.Dequeue();

            var core = cores.Find(c => c.NrNeurons + layer.NrNeurons <= c.MaxNrNeurons && c.AcceptedTypes.Contains(layer.Type));
            if (core != null)
            {
                core.NrNeurons += layer.NrNeurons;
                MappingFound?.Invoke(layer, core, false, 0, layer.NrNeurons);
                continue;
            }

            // if the layer is not splittable then we can simply not map
            // the layer to a core
            if (!layer.Splittable)
            {
                unmapped.Add(layer);
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
                unmapped.Add(layer);
                continue;
            }
            Debug.Assert(splitSet.Count > 0);

            // actually assign the splits to each core
            int neuronsMapped = 0;
            neuronsToMap = layer.NrNeurons;
            foreach (var c in splitSet)
            {
                int freeNeurons = c.MaxNrNeurons - c.NrNeurons;
                int sliceSize = Math.Min(freeNeurons, neuronsToMap);
                MappingFound?.Invoke(layer, c, true, neuronsMapped, neuronsMapped + sliceSize);
                c.NrNeurons += sliceSize;
                neuronsMapped += sliceSize;
                neuronsToMap -= sliceSize;
            }
            Debug.Assert(neuronsMapped == layer.NrNeurons);


        }

        return unmapped;
    }
}

public class MapCore
{
    // Params
    public object Value;
    public string Name;
    public int MaxNrNeurons;
    public List<int> AcceptedTypes;

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
    public int Type;

    public void Build()
    {

    }
}