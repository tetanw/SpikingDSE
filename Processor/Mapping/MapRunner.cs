using System.Text.Json;
using System.IO;

namespace SpikingDSE;

public class MapRunner
{
    private MappingOptions opts;

    public MapRunner(MappingOptions opts)
    {
        this.opts = opts;
    }

    public void Run()
    {
        var hw = HWSpec.Load(opts.HW);
        var snn = SRNN.Load(opts.SNN, 700, 2);
        Mapper mapper = null;
        if (opts.Mapper == "FirstFit")
        {
            mapper = new FirstFitMapper();
        }
        else
        {
            throw new System.Exception($"Unknown mapper: {opts.Mapper}");
        }
        var mapping = MultiCoreMapping.CreateMapping(mapper, hw, snn);
        mapping.Save(opts.Mapping);
    }
}