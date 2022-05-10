using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class AnalyzeMapping
{
    private readonly AnalyzeMappingOptions opts;

    public AnalyzeMapping(AnalyzeMappingOptions opts)
    {
        this.opts = opts;
    }

    public int Run()
    {
        var snn = SNN.Load(opts.SNN);
        var hw = HWSpec.Load(opts.HW);
        var mapping = Mapping.Load(opts.Mapping);

        var cores = hw.Cores.Select(c => new CoreData(c)).ToDictionary(c => c.Spec.Name);

        foreach (var entry in mapping.Mapped)
        {
            var layer = snn.FindLayer(entry.Layer);
            var core = cores[entry.Core];
            core.AddLayer(layer, entry.End - entry.Start);
        }

        foreach (var core in hw.Cores)
        {
            var data = cores[core.Name];
            Console.WriteLine($"Core \"{core.Name}\":");
            double neuronPer = (double)data.NrNeurons / core.MaxNeurons * 100.0;
            Console.WriteLine($"  Neurons: {data.NrNeurons} / {core.MaxNeurons} ({neuronPer:0.00}%)");
            double layerPer = (double)data.NrLayers / core.MaxLayers * 100.0;
            Console.WriteLine($"  Layers: {data.NrLayers} / {core.MaxLayers} ({layerPer:0.00}%)");
            double synPer = (double)data.NrSynapses / core.MaxSynapses * 100.0;
            Console.WriteLine($"  Synapses: {data.NrSynapses} / {core.MaxSynapses} ({synPer:0.00}%)");
            double fanInPer = (double)data.NrFanIn / core.MaxFanIn * 100.0;
            Console.WriteLine($"  FanIn: {data.NrFanIn} / {core.MaxFanIn} ({fanInPer:0.00}%)");
        }

        // FIXME: The accounting should actually keep track of the max splits per core
        // but that is difficult, maybe later
        var layerSplits = snn.GetAllLayers().ToDictionary(l => l, l => 0);
        foreach (var entry in mapping.Mapped)
        {
            layerSplits.AddCount(snn.FindLayer(entry.Layer), 1);
        }
        foreach (var (layer, splits) in layerSplits)
        {
            Console.WriteLine($"Layer \"{layer.Name}\": ");
            Console.WriteLine($"  Splits: {splits}");
        }

        if (mapping.Unmapped.Count > 0)
        {
            Console.WriteLine("Unmapped:");
            foreach (var layerName in mapping.Unmapped)
            {
                var layer = snn.FindLayer(layerName);
                Console.WriteLine($"  - {layer.Name}, Size: {layer.Size}, InputSize: {layer.InputSize}");
            }
        }

        return 0;
    }
}