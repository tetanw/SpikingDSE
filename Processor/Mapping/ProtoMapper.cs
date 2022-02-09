using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SpikingDSE;

public class ProtoMapper
{
    private SNN snn;
    private List<Core> cores;

    public Action<Core, Layer> OnMappingFound;

    public ProtoMapper(SNN snn, IEnumerable<Core> cores)
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


