using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public class CreateMapping
{
    private readonly CreateMappingOptions opts;

    public CreateMapping(CreateMappingOptions opts)
    {
        this.opts = opts;
    }

    public int Run()
    {
        var hw = HWSpec.Load(opts.HW);
        var snn = SNN.Load(opts.SNN);
        Mapper mapper;
        if (opts.Mapper == "FirstFit1")
        {
            mapper = new FirstFitMapper1(hw, snn);
        }
        else if (opts.Mapper == "FirstFit2")
        {
            mapper = new FirstFitMapper2(hw, snn);
        }
        else
        {
            throw new Exception($"Unknown mapper: {opts.Mapper}");
        }
        var mapping = mapper.Run();
        if (mapping.Unmapped.Count > 0)
        {
            var layerNames = string.Join(',', mapping.Unmapped);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Could not map: {layerNames}");
            mapping.Save(opts.Mapping);
            return 1;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Success");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Saving to: `{opts.Mapping}`");
            mapping.Save(opts.Mapping);
            return 0;
        }
    }
}