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

    public abstract class Model<T>
    {
        public abstract T Calculate(SpikeMap spikes);
    }

    public class MemoryModel : Model<Memory>
    {
        private HWConfig hw;

        public MemoryModel(HWConfig hw)
        {
            this.hw = hw;
        }

        public override Memory Calculate(SpikeMap spikes)
        {
            int NrSOPs = hw.NrNeurons * (spikes.Input.Count + spikes.Internal.Count);

            // Memory
            var memory = new Memory();
            memory.NeuronReads = NrSOPs / hw.MemNeuronBatchSize;
            memory.NeuronWrites = NrSOPs / hw.MemNeuronBatchSize;
            memory.SynReads = NrSOPs / hw.MemSynapseBatchSize;
            memory.SynWrites = NrSOPs / hw.MemSynapseBatchSize;
            return memory;
        }
    }

    public class LatencyModel : Model<Latency>
    {
        private HWConfig hw;

        public LatencyModel(HWConfig hw)
        {
            this.hw = hw;
        }

        public override Latency Calculate(SpikeMap spikes)
        {
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

            // Latency
            int NrSOPs = hw.NrNeurons * (spikes.Input.Count + spikes.Internal.Count);
            int Input = hw.InputLatency * spikes.Input.Count;
            int Output = hw.OutputLatency * spikes.Output.Count;
            int Compute = pipeII * NrSOPs + (spikes.Input.Count + spikes.Internal.Count) * (pipeLat - pipeII);
            int Internal = hw.InternalLatency * spikes.Internal.Count;
            return Latency.InCycles(Input, Internal, Output, Compute, hw.Frequency);
        }
    }

    public class EnergyModel : Model<Energy>
    {
        private HWConfig hw;
        private CostConfig cost;
        private Memory memory;
        private Latency latency;

        public EnergyModel(HWConfig hw, CostConfig cost, Memory memory, Latency latency)
        {
            this.hw = hw;
            this.cost = cost;
            this.memory = memory;
            this.latency = latency;
        }

        public override Energy Calculate(SpikeMap spikes)
        {
            // Energy
            var energy = new Energy();
            int NrSOPs = hw.NrNeurons * (spikes.Input.Count + spikes.Internal.Count);

            double timeActive = latency.Total.Secs;
            energy.Core = new EnergyMetric(
                leakage: timeActive * hw.NrCores * cost.CoreLeakage,
                dynamic: NrSOPs * cost.CoreDynamic
            );

            energy.Router = new EnergyMetric(
                leakage: timeActive * cost.RouterLeakage,
                dynamic: (spikes.Input.Count + spikes.Output.Count) * cost.RouterDynamic
            );

            energy.Scheduler = new EnergyMetric(
                leakage: timeActive * hw.SchedulerBufferSize * cost.BufferLeakage,
                dynamic: (spikes.Input.Count + spikes.Internal.Count) * hw.SchedulerBufferSize * cost.BufferDynamic
            );

            energy.Controller = new EnergyMetric(
                leakage: timeActive * cost.ControllerLeakage,
                dynamic: 0.0
            );

            energy.NeuronMem = new EnergyMetric(
                leakage: timeActive * cost.MemNeuronLeakage,
                dynamic: memory.NeuronReads * cost.MemNeuronReadEnergy
                + memory.NeuronWrites * cost.MemNeuronWriteEnergy
            );

            energy.SynMem = new EnergyMetric(
                leakage: timeActive * cost.MemSynapseLeakage,
                dynamic: memory.SynReads * cost.MemSynReadEnergy
                + memory.SynWrites * cost.MemSynWriteEnergy
            );
            return energy;
        }
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

    public class PEReport
    {
        public PEReport(int coreID, int TS, SpikeMap spikes, Memory memory, Latency latency, Energy energy)
        {
            this.CoreID = coreID;
            this.TS = TS;
            Type = "PE";

            this.Spikes = spikes;
            this.Memory = memory;
            this.Latency = latency;
            this.Energy = energy;
        }

        public int CoreID { get; private set; }
        public int TS { get; private set; }
        public string Type { get; private set; }

        public SpikeMap Spikes { get; private set; }
        public Memory Memory { get; private set; }
        public Latency Latency { get; private set; }
        public Energy Energy { get; private set; }

    }


    public class TimestepReport
    {
        public TimestepReport(int TS)
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

    public class SimReport
    {
        public SimReport()
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

    public class Analysis
    {
        public List<object> Reports { get; set; } = new List<object>();
    }
}