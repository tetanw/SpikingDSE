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

    class Program
    {
        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<SimOptions, VCDOptions, ToTensorOptions>(args);
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
                        new ProducerConsumer().Run();
                    }
                    else if (opts.Name.Equals("ODIN"))
                    {
                        new ODINSingleCore().Run();
                    }
                    else if (opts.Name.Equals("MESHTEST"))
                    {
                        new MeshTest().Run();
                    }
                    else if (opts.Name.Equals("FORKJOIN"))
                    {
                        new ForkJoin().Run();
                    }
                    else if (opts.Name.Equals("REPTEST"))
                    {
                        new ReportingTest().Run();
                    }
                    else if (opts.Name.Equals("MULTIODIN"))
                    {
                        new MultiODINTest().Run();
                    }
                    else if (opts.Name.Equals("RESTEST"))
                    {
                        new ResTest().Run();
                    }
                    else if (opts.Name.Equals("RESPERF"))
                    {
                        new ResPerf().Run();
                    }
                    else if (opts.Name.Equals("ToyProblem"))
                    {
                        new ToyProblem().Run();
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
                _ => 1
            );

            return ret;
        }
    }
}