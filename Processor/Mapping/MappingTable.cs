using System.Collections.Generic;

namespace SpikingDSE;

public class MappingTable
{
    private readonly Dictionary<ICore, List<Layer>> coreToLayer = new();
    private readonly Dictionary<Layer, ICore> layerToCore = new();
    private readonly SNN snn;

    public List<ICore> Cores { get; set; } = new();

    public MappingTable(SNN snn)
    {
        this.snn = snn;
    }

    public void Map(ICore core, Layer layer)
    {
        if (coreToLayer.TryGetValue(core, out List<Layer> layers))
        {
            layers.Add(layer);
        }
        else
        {
            coreToLayer.Add(core, new List<Layer>() { layer });
        }
        layerToCore.Add(layer, core);
    }

    public IEnumerable<Layer> LayersOf(ICore core)
    {
        return coreToLayer[core];
    }

    public IEnumerable<ICore> MappedCores
    {
        get => coreToLayer.Keys;
    }

    public IEnumerable<Layer> Layers
    {
        get => layerToCore.Keys;
    }

    public IEnumerable<KeyValuePair<Layer, ICore>> Pairs
    {
        get => layerToCore;
    }

    public object CoordOf(Layer layer)
    {
        return layerToCore[layer].GetLocation();
    }

    public object ControllerCoord { get; set; }

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

    public List<Layer> GetAllLayers(ICore core)
    {
        return coreToLayer.Optional(core) ?? new List<Layer>();
    }
}