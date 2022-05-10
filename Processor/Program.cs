using System;
using System.Diagnostics;
using System.Linq;
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

    [Verb("mapping", HelpText = "Maps a SNN on HW")]
    public class CreateMappingOptions
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

    [Verb("analyze-mapping", HelpText = "Maps a SNN on HW")]
    public class AnalyzeMappingOptions
    {
        [Option('s', "snn-path", Required = true, HelpText = "SNN specification")]
        public string SNN { get; set; }

        [Option('h', "hw-path", Required = true, HelpText = "HW specification")]
        public string HW { get; set; }

        [Option('m', "mapping", Required = true, HelpText = "Mapping to analyze")]
        public string Mapping { get; set; }
    }

    [Verb("trace-sim", HelpText = "Test a single input trace on HW")]
    public class SimTestOptions
    {
        [Option('s', "snn-path", Required = true, HelpText = "SNN specification")]
        public string SNN { get; set; }

        [Option('h', "hw-path", Required = true, HelpText = "HW specification")]
        public string HW { get; set; }

        [Option('m', "mapping", Required = true, HelpText = "File to read the mapping from")]
        public string Mapping { get; set; }

        [Option('t', "trace", Required = true, HelpText = "Trace to extract from")]
        public string Trace { get; set; }

        [Option('o', "output", Required = true, HelpText = "Folder to save the results in")]
        public string Output { get; set; }
    }

    [Verb("dataset-sim", HelpText = "Test a whole dataset on HW")]
    public class SimDSEOptions
    {
        [Option('s', "snn-path", Required = true, HelpText = "SNN specification")]
        public string SNN { get; set; }

        [Option('h', "hw-path", Required = true, HelpText = "HW specification")]
        public string HW { get; set; }

        [Option('m', "mapping", Required = true, HelpText = "File to save the mapping in")]
        public string Mapping { get; set; }

        [Option('d', "dataset", Required = true, HelpText = "The dataset to run")]
        public string Dataset { get; set; }

        [Option("max-samples", Required = false, HelpText = "Max samples to run through")]
        public int MaxSamples { get; set; } = int.MaxValue;

        [Option('o', "output-dir", Required = true, HelpText = "Director to place results")]
        public string OutputDir { get; set; }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<SimOptions, SimTestOptions, SimDSEOptions, VCDOptions, ToTensorOptions, CreateMappingOptions, AnalyzeMappingOptions>(args);
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
                (SimTestOptions opts) =>
                {
                    var split = opts.Trace.Split(";");
                    var datasetPath = split[0];
                    var traceName = split[1];
                    new MultiCoreTrace(opts.SNN, opts.HW, opts.Mapping, datasetPath, traceName, opts.Output).Run();
                    return 0;
                },
                (SimDSEOptions opts) =>
                {
                    System.Diagnostics.Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                    new MultiCoreDataset(opts.SNN, opts.HW, opts.Mapping, opts.Dataset, opts.OutputDir, opts.MaxSamples).Run();
                    return 0;
                },
                (ToTensorOptions opts) =>
                {
                    var converter = new ToTensor(opts.TensorInput, opts.EventInput, opts.TensorOut);
                    converter.Run();
                    return 0;
                },
                (CreateMappingOptions opts) => new CreateMapping(opts).Run(),
                (AnalyzeMappingOptions opts) => new AnalyzeMapping(opts).Run(),
                _ => 1
            );

            return ret;
        }
    }
}