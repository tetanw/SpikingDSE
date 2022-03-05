using System.Collections.Generic;

namespace SpikingDSE;

public class MappingTable
{
    private Dictionary<Core, List<Layer>> coreToLayer = new();
    private Dictionary<Layer, Core> layerToCore = new();
    private SNN snn;

    public MappingTable(SNN snn)
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

    public IEnumerable<Layer> LayersOf(Core core)
    {
        return this.coreToLayer[core];
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

    public object CoordOf(Layer layer)
    {
        return this.layerToCore[layer].GetLocation();
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

    public List<Layer> GetAllLayers(Core core)
    {
        return coreToLayer[core];
    }
}