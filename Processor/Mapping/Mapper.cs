using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SpikingDSE;

public class FirstFitMapper
{
    private SNN snn;
    private List<Core> cores;

    public Action<Core, Layer> OnMappingFound;

    public FirstFitMapper(SNN snn, IEnumerable<Core> cores)
    {
        this.snn = snn;
        this.cores = cores.ToList();
    }

    public void Run()
    {
        foreach (var layer in snn.GetAllLayers())
        {
            var core = cores.Find((core) => core.AcceptsLayer(layer));
            if (core is not null)
            {
                core.AddLayer(layer);
                OnMappingFound?.Invoke(core, layer);
            }
            else
            {
                throw new Exception("Could not find core for layer!");
            }
        }
    }
}

public class Mapping
{
    private Dictionary<Core, List<Layer>> coreToLayer = new();
    private Dictionary<Layer, Core> layerToCore = new();
    private SNN snn;

    public Mapping(SNN snn)
    {
        this.snn = snn;
    }

    public void Map(Core core, Layer layer)
    {
        List<Layer> layers;
        if (this.coreToLayer.TryGetValue(core, out layers))
        {
            layers.Add(layer);
        }
        else
        {
            this.coreToLayer.Add(core, new List<Layer>() { layer });
        }
        this.layerToCore.Add(layer, core);
    }

    public IEnumerable<Layer> this[Core core]
    {
        get => this.coreToLayer[core];
    }

    public Core this[Layer layer]
    {
        get => this.layerToCore[layer];
    }

    public IEnumerable<Core> Cores
    {
        get => this.coreToLayer.Keys;
    }

    public IEnumerable<Layer> Layers
    {
        get => this.layerToCore.Keys;
    }

    public IEnumerable<KeyValuePair<Layer, Core>> Pairs
    {
        get => this.layerToCore;
    }

    public MeshCoord CoordOf(Layer layer)
    {
        return (MeshCoord)this.layerToCore[layer].GetLocation();
    }

    public Layer GetDestLayer(Layer layer)
    {
        return snn.GetDestLayer(layer);
    }

    public IEnumerable<Layer> GetDestLayers(Layer layer)
    {
        return snn.GetDestLayers(layer);
    }

    public IEnumerable<Layer> GetSiblings(Layer layer)
    {
        return snn.GetSiblingLayers(layer);
    }
}

public interface Core
{
    public bool AcceptsLayer(Layer layer);
    public void AddLayer(Layer layer);
    public InPort GetIn();
    public OutPort GetOut();
    public object GetLocation();
}
