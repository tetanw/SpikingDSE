using System;
using CommandLine;

namespace SpikingDSE
{


    [Verb("sim", HelpText = "Simulate trace file")]
    public class SimOptions
    {
        [Value(0)]
        public string Name { get; set; }
    }

    [Verb("parse-vcd", HelpText = "Parse VCD file")]
    public class VCDOptions
    {
        [Option('i', "input", Required = true, HelpText = "Input VCD file")]
        public string Input { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output trace file")]
        public string Output { get; set; }

        [Option('l', "limit", HelpText = "Max amount of lines to read")]
        public int MaxLines { get; set; }
    }

    [Verb("to-tensor", HelpText = "Converts event trace to tensor")]
    public class ToTensorOptions
    {
        [Option('t', "tensor-input", Required = true, HelpText = "Tensor to be used as reference")]
        public string TensorInput { get; set; }

        [Option('e', "event-input", Required = true, HelpText = "Events that need to be to tensor")]
        public string EventInput { get; set; }

        [Option('o', "tensor-out", Required = true, HelpText = "Output tensor that is to be created")]
        public string TensorOut { get; set; }
    }

    [Verb("trace", HelpText = "Converts srnn into a trace")]
    public class TraceGeneratorOptions
    {

    }

    [Verb("mapping", HelpText = "Maps a SNN on HW")]
    public class MappingOptions
    {
        [Option('s', "snn-path", Required = true, HelpText = "SNN specification")]
        public string SNN { get; set; }

        [Option('h', "hw-path", Required = true, HelpText = "HW specification")]
        public string HW { get; set; }

        [Option('m', "mapper", Required = true, HelpText = "Mapper to use")]
        public string Mapper { get; set; }

        [Option('o', "mapping", Required = true, HelpText = "File to save the mapping in")]
        public string Mapping { get; set; }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<SimOptions, VCDOptions, ToTensorOptions, TraceGeneratorOptions, MappingOptions>(args);
            var ret = result.MapResult(
                (VCDOptions opts) =>
                {
                    var vcd = new TraceNeuronVCD(opts.Input, opts.Output);
                    vcd.Process();
                    return 0;
                },
                (SimOptions opts) =>
                {
                    if (opts.Name.Equals("PC"))
                    {
                        new PC().Run();
                    }
                    else if (opts.Name.Equals("MeshTest"))
                    {
                        new MeshTest().Run();
                    }
                    else if (opts.Name.Equals("ForkJoin"))
                    {
                        new ForkJoin().Run();
                    }
                    else if (opts.Name.Equals("RepTest"))
                    {
                        new RepTest().Run();
                    }
                    else if (opts.Name.Equals("ResTest"))
                    {
                        new ResTest().Run();
                    }
                    else if (opts.Name.Equals("ResPerf"))
                    {
                        new ResPerf().Run();
                    }
                    else if (opts.Name.Equals("ToyProblem"))
                    {
                        new ToyProblem().Run();
                    }
                    else if (opts.Name.Equals("SingleOdin"))
                    {
                        new SingleOdin().Run();
                    }
                    else if (opts.Name.Equals("MultiCoreTest"))
                    {
                        new MultiCoreTest().Run();
                    }
                    else if (opts.Name.Equals("MultiCoreDSE"))
                    {
                        new MultiCoreDSE().Run();
                    }
                    else if (opts.Name.Equals("XYRouterTest"))
                    {
                        new XYRouterTest().Run();
                    }
                    else if (opts.Name.Equals("MapperTest"))
                    {
                        new MapperTest().Run();
                    }
                    else if (opts.Name.Equals("XYRouterValidation"))
                    {
                        new XYRouterValidation().Run();
                    }
                    else
                    {
                        throw new Exception($"Unknown simulation: {opts.Name}");
                    }
                    return 0;
                },
                (ToTensorOptions opts) =>
                {
                    var converter = new ToTensor(opts.TensorInput, opts.EventInput, opts.TensorOut);
                    converter.Run();
                    return 0;
                },
                (TraceGeneratorOptions opts) =>
                {
                    new TraceGenerator().Run();
                    return 0;
                },
                (MappingOptions opts) =>
                {
                    new MapRunner(opts).Run();
                    return 0;
                },
                _ => 1
            );

            return ret;
        }
    }
}