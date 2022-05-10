using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public class FirstFitMapper2 : ConstraintMapper
{
    public FirstFitMapper2(HWSpec hw, SNN snn) : base(hw, snn)
    {

    }

    public override Mapping Run()
    {
        var mapping = new Mapping();
        var layers = snn.GetAllLayers();

        // Do the actual mapping
        var unmapped = new Queue<Layer>(layers);
        var sortedCores = hw.Cores.Select(c => new CoreData(c)).ToList();
        sortedCores.Sort(CorePriorityCmp);
        while (unmapped.Count > 0)
        {
            var layer = unmapped.Dequeue();

            if (!layer.Splittable)
            {
                var (core, mappedLayer) = FindWholeFit(sortedCores, layer);
                if (core != null)
                {
                    core.AddLayer(layer);
                    mapping.Mapped.Add(mappedLayer);
                    continue;
                }
                else
                {
                    mapping.Unmapped.Add(layer.Name);
                    continue;
                }
            }
            else
            {
                var splitSet = FindSplitSet(sortedCores, layer);
                if (splitSet == null)
                {
                    mapping.Unmapped.Add(layer.Name);
                    continue;
                }
                else
                {
                    // actually assign the splits to each core
                    AssignSplitSet(splitSet, layer, mapping);
                }
            }
        }
        return mapping;
    }
}