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
            return $"Leakage: {Measurements.FormatSI(Leakage, "J")} Dynamic: {Measurements.FormatSI(Dynamic, "J")}, Total: {Measurements.FormatSI(Total, "J")}";
        }

    }

    public class Energy
    {
        [JsonIgnore]
        public double Time { get; set; }

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
        public double Power
        {
            get
            {
                double value = Total.Total / Time;
                value = double.IsInfinity(value) ? double.NaN : value;
                return value;
            }
        }

        public static Energy operator +(Energy a, Energy b)
        {
            var res = new Energy();
            res.Time = a.Time;
            res.Core = a.Core + b.Core;
            res.Router = a.Router + b.Router;
            res.Scheduler = a.Scheduler + b.Scheduler;
            res.Controller = a.Controller + b.Controller;
            res.NeuronMem = a.NeuronMem + b.NeuronMem;
            res.SynMem = a.SynMem + b.SynMem;
            return res;
        }
    }

    public class Latency
    {
        [JsonIgnore]
        public double Frequency { get; set; }

        public int Input { get; set; }
        public int Internal { get; set; }
        public int Output { get; set; }
        public int Compute { get; set; }
        public int Total { get => Input + Internal + Output + Compute; }
        public double TotalSecs { get => Total / Frequency; }

        public static Latency operator +(Latency a, Latency b)
        {
            var res = new Latency();
            res.Frequency = a.Frequency;
            res.Input = a.Input + b.Input;
            res.Internal = a.Internal + b.Internal;
            res.Output = a.Output + b.Output;
            res.Compute = a.Compute + b.Compute;
            return res;
        }
    }

    public class PEAnalysis
    {
        public PEAnalysis(int coreID, int TS, SpikeMap spikes, HWConfig hw, CostConfig cost)
        {
            this.CoreID = coreID;
            this.TS = TS;
            Type = "PE";

            int pipeII = 0, pipeLat = 0;
            if (hw.PipelineII != 0 && hw.PipelineLat != 0)
            {
                pipeII = hw.PipelineII;
                pipeLat = hw.PipelineLat;
            }
            else
            {
                throw new Exception("Can not determine pipeline latency and II");
            }

            // General
            this.Spikes = spikes;
            NrSOPs = hw.NrNeurons * (spikes.Input.Count + spikes.Internal.Count);

            // Memory
            NeuronMemReads = NrSOPs / hw.MemNeuronBatchSize;
            NeuronMemWrites = NrSOPs / hw.MemNeuronBatchSize;
            SynMemReads = NrSOPs / hw.MemSynapseBatchSize;
            SynMemWrites = NrSOPs / hw.MemSynapseBatchSize;

            // Latency
            Latency = new Latency();
            Latency.Frequency = hw.Frequency;
            Latency.Input = hw.InputLatency * spikes.Input.Count;
            Latency.Output = hw.OutputLatency * spikes.Output.Count;
            Latency.Compute = pipeII * NrSOPs + (spikes.Input.Count + spikes.Internal.Count) * (pipeLat - pipeII);
            Latency.Internal = hw.InternalLatency * spikes.Internal.Count;

            // Energy
            Energy = new Energy();
            Energy.Time = Latency.TotalSecs;

            Energy.Core = new EnergyMetric(
                leakage: Latency.TotalSecs * hw.NrCores * cost.CoreLeakage,
                dynamic: NrSOPs * cost.CoreDynamic
            );

            Energy.Router = new EnergyMetric(
                leakage: Latency.TotalSecs * cost.RouterLeakage,
                dynamic: (spikes.Input.Count + spikes.Output.Count) * cost.RouterDynamic
            );

            Energy.Scheduler = new EnergyMetric(
                leakage: Latency.TotalSecs * hw.SchedulerBufferSize * cost.BufferLeakage,
                dynamic: (spikes.Input.Count + spikes.Internal.Count) * hw.SchedulerBufferSize * cost.BufferDynamic
            );

            Energy.Controller = new EnergyMetric(
                leakage: Latency.TotalSecs * cost.ControllerLeakage,
                dynamic: 0.0
            );

            Energy.NeuronMem = new EnergyMetric(
                leakage: Latency.TotalSecs * cost.MemNeuronLeakage,
                dynamic: NeuronMemReads * cost.MemNeuronReadEnergy
                + NeuronMemWrites * cost.MemNeuronWriteEnergy
            );

            Energy.SynMem = new EnergyMetric(
                leakage: Latency.TotalSecs * cost.MemSynapseLeakage,
                dynamic: SynMemReads * cost.MemSynReadEnergy
                + SynMemWrites * cost.MemSynWriteEnergy
            );
        }

        public int CoreID { get; private set; }
        public int TS { get; private set; }
        public string Type { get; private set; }
        public int NrSOPs { get; private set; }
        public int NeuronMemReads { get; private set; }
        public int NeuronMemWrites { get; private set; }
        public int SynMemReads { get; private set; }
        public int SynMemWrites { get; private set; }

        public SpikeMap Spikes { get; private set; }
        public Latency Latency { get; private set; }
        public Energy Energy { get; private set; }

    }


    public class TimestepAnalysis
    {
        public TimestepAnalysis(int TS)
        {
            this.TS = TS;
            this.Type = "Timestep";
        }

        public string Type { get; private set; }
        public int TS { get; private set; }

        public Latency Latency { get; set; } = new Latency();
        public Energy Energy { get; set; } = new Energy();
        public List<SpikeRoute> SpikeRoutes { get; set; }
    }

    public class SimAnalysis
    {
        public SimAnalysis()
        {
            this.Type = "Sim";
        }

        public string Type { get; private set; }

        public Latency Latency { get; set; } = new Latency();
        public Energy Energy { get; set; } = new Energy();
    }

    public class MappingReport
    {
        public MappingReport()
        {
            Type = "Mapping";
        }

        public string Type { get; set; }
        public int[] Mapping { get; set; }
    }

    public class AnalysisReport
    {
        public List<object> Analyses { get; set; } = new List<object>();
    }
}