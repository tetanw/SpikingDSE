using System;
using CommandLine;

namespace SpikingDSE
{


    [Verb("sim", HelpText = "Simulate trace file")]
    public class SimOptions
    {
        [Option('i', "input", Required = true, HelpText = "Trace file that needs to be run")]
        public string TracePath { get; set; }

        [Option('h', "hw", Required = true, HelpText = "Hardware definition")]
        public string HwPath { get; set; }

        [Option('p', "profile", Required = false, HelpText = "Output profile information")]
        public string ProfPath { get; set; }
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

    [Verb("analyze", HelpText = "Analyzes input file")]
    public class AnalyzeOptions
    {
        [Option('s', "snn", Required = true, HelpText = "SNN definition")]
        public string SNN { get; set; }

        [Option('h', "hardware", Required = true, HelpText = "Hardware definition")]
        public string HW { get; set; }
        
        [Option('c', "cost", Required = true, HelpText = "Cost definition")]
        public string Cost { get; set; }

        [Option("strategy", Required = true, HelpText = "Strategy")]
        public string Strategy { get; set; }

        [Option('l', "limit", HelpText = "Max amount of timesteps to do")]
        public int MaxTimesteps { get; set; }
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

    class Program
    {
        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<SimOptions, VCDOptions, AnalyzeOptions, ToTensorOptions>(args);
            var ret = result.MapResult(
                (SimOptions opts) =>
                {
                    Console.WriteLine($"Trace file: '{opts.TracePath}'");
                    var sim = new Simulator(opts);
                    sim.Simulate();
                    return 0;
                },
                (VCDOptions opts) =>
                {
                    var vcd = new TraceNeuronVCD(opts.Input, opts.Output);
                    vcd.Process();
                    return 0;
                },
                (AnalyzeOptions opts) =>
                {
                    var analyzer = new Analyzer(opts.SNN, opts.HW, opts.Cost, opts.Strategy);
                    analyzer.Run(opts.MaxTimesteps);
                    return 0;
                },
                (ToTensorOptions opts) =>
                {
                    var converter = new ToTensor(opts.TensorInput, opts.EventInput, opts.TensorOut);
                    converter.Run();
                    return 0;
                },
                _ => 1
            );

            return ret;
        }
    }
}