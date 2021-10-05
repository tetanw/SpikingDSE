using SpikingDSE;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpikingDSE
{
    public class SpikeMap
    {
        public List<int> Input { get; set; } = new List<int>();
        public List<int> Internal { get; set; } = new List<int>();
        public List<int> Output { get; set; } = new List<int>();
    }

    public struct EnergyMetric
    {
        public EnergyMetric(double leakage, double dynamic)
        {
            Leakage = leakage;
            Dynamic = dynamic;
            Total = leakage + dynamic;
        }

        public double Leakage { get; }
        public double Dynamic { get; }
        public double Total { get; }

        public static EnergyMetric operator +(EnergyMetric a, EnergyMetric b)
        {
            return new EnergyMetric(
                leakage: a.Leakage + b.Leakage,
                dynamic: a.Dynamic + b.Dynamic
            );
        }

        public override String ToString()
        {
            return $"Leakage: {Measurements.FormatSI(Leakage, "J")}"
            + $"Dynamic: {Measurements.FormatSI(Dynamic, "J")}"
            + $"Total: {Measurements.FormatSI(Total, "J")}";
        }

    }

    public class Energy
    {
        public EnergyMetric Core { get; set; }
        public EnergyMetric Router { get; set; }
        public EnergyMetric Scheduler { get; set; }
        public EnergyMetric Controller { get; set; }
        public EnergyMetric NeuronMem { get; set; }
        public EnergyMetric SynMem { get; set; }
        public EnergyMetric Total
        {
            get => Core + Router + Scheduler + Controller + NeuronMem + SynMem;
        }

        public static Energy operator +(Energy a, Energy b)
        {
            var res = new Energy();
            res.Core = a.Core + b.Core;
            res.Router = a.Router + b.Router;
            res.Scheduler = a.Scheduler + b.Scheduler;
            res.Controller = a.Controller + b.Controller;
            res.NeuronMem = a.NeuronMem + b.NeuronMem;
            res.SynMem = a.SynMem + b.SynMem;
            return res;
        }
    }

    public class Memory
    {
        public int NeuronReads { get; set; }
        public int NeuronWrites { get; set; }
        public int SynReads { get; set; }
        public int SynWrites { get; set; }
    }

    public struct LatencyMetric
    {
        public LatencyMetric(int cycles, double secs)
        {
            this.Cycles = cycles;
            this.Secs = secs;
        }

        public int Cycles { get; }
        public double Secs { get; }

        public static LatencyMetric operator +(LatencyMetric a, LatencyMetric b)
        {
            return new LatencyMetric(
                a.Cycles + b.Cycles,
                a.Secs + b.Secs
            );
        }
    }

    public class Latency
    {
        public static Latency InCycles(int input, int intern, int output, int compute, long frequency)
        {
            var latency = new Latency();
            latency.Input = new LatencyMetric(input, (double)input / frequency);
            latency.Internal = new LatencyMetric(input, (double)intern / frequency);
            latency.Output = new LatencyMetric(input, (double)output / frequency);
            latency.Compute = new LatencyMetric(input, (double)compute / frequency);
            return latency;
        }

        public LatencyMetric Input { get; private set; }
        public LatencyMetric Internal { get; private set; }
        public LatencyMetric Output { get; private set; }
        public LatencyMetric Compute { get; private set; }
        public LatencyMetric Total { get => Input + Internal + Output + Compute; }

        public static Latency operator +(Latency a, Latency b)
        {
            var res = new Latency();
            res.Input = a.Input + b.Input;
            res.Internal = a.Internal + b.Internal;
            res.Output = a.Output + b.Output;
            res.Compute = a.Compute + b.Compute;
            return res;
        }
    }


}