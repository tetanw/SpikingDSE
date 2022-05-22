using System.Collections.Generic;

namespace SpikingDSE;

public class MappingManager
{
    private readonly Dictionary<Core, List<Layer>> coreToLayer = new();
    private readonly Dictionary<Layer, Core> layerToCore = new();
    private readonly SNN snn;

    public List<Core> Cores { get; set; } = new();

    public MappingManager(SNN snn)
    {
        this.snn = snn;
    }

    public void Map(Core core, Layer layer)
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

    public IEnumerable<Layer> LayersOf(Core core)
    {
        return coreToLayer[core];
    }

    public IEnumerable<Core> MappedCores
    {
        get => coreToLayer.Keys;
    }

    public IEnumerable<Layer> Layers
    {
        get => layerToCore.Keys;
    }

    public IEnumerable<KeyValuePair<Layer, Core>> Pairs
    {
        get => layerToCore;
    }

    public InputLayer GetInputLayer()
    {
        return snn.GetInputLayer();
    }

    public object CoordOf(Layer layer)
    {
        return layerToCore[layer].Location;
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

    public List<Layer> GetAllLayers(Core core)
    {
        return coreToLayer.Optional(core) ?? new List<Layer>();
    }
}