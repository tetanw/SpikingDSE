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
        Mapper mapper;
        if (opts.Mapper == "FirstFit")
        {
            mapper = new FirstFitMapper(hw, snn);
        }
        else
        {
            throw new Exception($"Unknown mapper: {opts.Mapper}");
        }
        var mapping = mapper.Run();
        if (mapping.Unmapped.Count > 0)
        {
            var layerNames = string.Join(',', mapping.Unmapped);
            throw new Exception($"Could not map: {layerNames}");
        }   
        mapping.Save(opts.Mapping);
    }
}