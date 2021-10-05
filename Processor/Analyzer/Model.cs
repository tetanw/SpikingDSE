using System;

namespace SpikingDSE
{
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
}