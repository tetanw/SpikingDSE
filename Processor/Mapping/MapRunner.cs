using System.Text.Json;
using System.IO;
using System.Linq;
using System;

namespace SpikingDSE;

public class MapRunner
{
    private readonly MappingOptions opts;

    public MapRunner(MappingOptions opts)
    {
        this.opts = opts;
    }

    public void Run()
    {
        var hw = HWSpec.Load(opts.HW);
        var snn = SNN.Load(opts.SNN);
        IMapper mapper;
        if (opts.Mapper == "FirstFit")
        {
            mapper = new FirstFitMapper();
        }
        else
        {
            throw new Exception($"Unknown mapper: {opts.Mapper}");
        }
        var mapping = MultiCoreMapping.CreateMapping(mapper, hw, snn);
        if (mapping.Unmapped.Count > 0)
        {
            var layerNames = string.Join(',', mapping.Unmapped.Select(l => l.Name).ToArray());
            throw new Exception($"Could not map: {layerNames}");
        }   
        mapping.Save(opts.Mapping);
    }
}