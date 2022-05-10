using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;


public class LayerData
{
    public Layer Layer { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public int Index { get; set; }
}

public abstract class ConstraintMapper : Mapper
{
    protected ConstraintMapper(HWSpec hw, SNN snn) : base(hw, snn)
    {
    }

    protected static int CorePriorityCmp(CoreData c1, CoreData c2) => c1.Spec.Priority < c2.Spec.Priority ? 1 : -1;

    protected static (CoreData, MappedLayer) FindWholeFit(List<CoreData> cores, Layer layer)
    {
        var core = cores.Find(c => c.FitsLayer(layer));
        if (core != null)
        {
            return (core, new MappedLayer
            {
                Layer = layer.Name,
                Core = core.Spec.Name,
                Partial = false,
                Start = 0,
                End = layer.Size,
                Index = 0
            });
        }
        else
        {
            return (null, null);
        }
    }

    protected static Dictionary<CoreData, LayerData> FindSplitSet(List<CoreData> cores, Layer layer)
    {
        Dictionary<CoreData, LayerData> splitSet = new();
        int i = 1;
        int mappedNeurons = 0;
        foreach (var c in cores)
        {
            int neuronsToMap = layer.Size - mappedNeurons;
            int toMap = c.MaximumCut(layer, neuronsToMap);
            if (toMap == 0)
                continue;

            splitSet.Add(c, new LayerData
            {
                Layer = layer,
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
            return null;
        }
        int maxSplits = splitSet.Min(c => c.Key.Spec.MaxSplits);
        if (splitSet.Count >= maxSplits)
        {
            // the layer is splitted in too many parts
            return null;
        }
        Debug.Assert(splitSet.Count > 0);
        Debug.Assert(mappedNeurons == layer.Size);
        return splitSet;
    }

    protected static void AssignSplitSet(Dictionary<CoreData, LayerData> splitSet, Layer layer, Mapping mapping)
    {
        foreach (var (c, l) in splitSet)
        {
            mapping.Mapped.Add(new MappedLayer
            {
                Layer = layer.Name,
                Core = c.Spec.Name,
                Partial = true,
                Start = l.Start,
                End = l.End,
                Index = l.Index
            });
            c.AddLayer(l.Layer, l.End - l.Start);
        }
    }

}