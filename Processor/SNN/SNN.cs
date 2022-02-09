using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class SNN
{
    public Dictionary<Layer, HashSet<Layer>> outputs = new();
    public Dictionary<Layer, HashSet<Layer>> inputs = new();
    public Dictionary<Layer, List<Layer>> siblings = new();
    public HashSet<Layer> layers = new();

    public Layer FindLayer(string name)
    {
        return layers.FirstOrDefault(l => l.Name == name);
    }

    public void AddForward(Layer from, Layer to)
    {
        HashSet<Layer> l;
        if (outputs.TryGetValue(from, out l))
        {
            l.Add(to);
        }
        else
        {
            outputs[from] = new() { to };
        }

        if (inputs.TryGetValue(to, out l))
        {
            l.Add(from);
        }
        else
        {
            inputs[to] = new() { from };
        }

        layers.Add(from);
        layers.Add(to);
    }

    public void GroupSiblings(IEnumerable<Layer> layers)
    {
        var layerList = layers.ToList();
        foreach (var layer in layerList)
        {
            siblings[layer] = layerList;
        }
    }

    public IEnumerable<Layer> GetAllLayers()
    {
        return layers;
    }

    public Layer GetSourceLayer(Layer layer)
    {
        return GetSourceLayers(layer)?.First();
    }

    public Layer GetDestLayer(Layer layer)
    {
        return GetDestLayers(layer)?.First();
    }

    public IEnumerable<Layer> GetDestLayers(Layer layer)
    {
        HashSet<Layer> outVal = null;
        outputs.TryGetValue(layer, out outVal);
        return outVal;
    }

    public IEnumerable<Layer> GetSourceLayers(Layer layer)
    {
        HashSet<Layer> outVal = null;
        inputs.TryGetValue(layer, out outVal);
        return outVal;
    }

    public IEnumerable<Layer> GetSiblingLayers(Layer layer)
    {
        List<Layer> outVal = null;
        siblings.TryGetValue(layer, out outVal);
        return outVal ?? Enumerable.Empty<Layer>();
    }
}