using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class AnalyzeMappings
{
    class CoreData
    {
        public string Name;
        public int Neurons = 0;
        public int Synapses = 0;
        public int Layers = 0;
    }

    private readonly AnalyzeMappingOptions opts;

    public AnalyzeMappings(AnalyzeMappingOptions opts)
    {
        this.opts = opts;
    }

    public int Run()
    {
        var snn = SNN.Load(opts.SNN);
        var hw = HWSpec.Load(opts.HW);
        var mapping = Mapping.Load(opts.Mapping);

        var cores = hw.Cores.Select(c => new CoreData { Name = c.Name }).ToDictionary(c => c.Name);

        foreach (var entry in mapping.Mapped)
        {
            var core = cores[entry.Core];
            int slice = entry.End - entry.Start;
            core.Neurons += slice;
            core.Layers++;
            var layer = snn.FindLayer(entry.Layer);
            core.Synapses += layer.InputSize * slice;
            Console.WriteLine($"{entry.Layer}: {layer.InputSize}, {layer.InputSize * slice}, {layer.Size * slice}");
            if (layer.Recurrent)
                core.Synapses += layer.Size * slice;
        }

        foreach (var core in hw.Cores)
        {
            var data = cores[core.Name];
            Console.WriteLine($"Core \"{core.Name}\":");
            double neuronPer = (double)data.Neurons / core.MaxNeurons * 100.0;
            Console.WriteLine($"  Neurons: {data.Neurons} / {core.MaxNeurons} ({neuronPer:0.00}%)");
            double layerPer = (double)data.Layers / core.MaxLayers * 100.0;
            Console.WriteLine($"  Layers: {data.Layers} / {core.MaxLayers} ({layerPer:0.00}%)");
            double synPer = (double)data.Synapses / core.MaxSynapses * 100.0;
            Console.WriteLine($"  Synapses: {data.Synapses} / {core.MaxSynapses} ({synPer:0.00}%)");
        }

        return 0;
    }
}