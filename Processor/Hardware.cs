namespace SpikingDSE
{
    public class HWConfig
    {
        // HW
        public int NrPEs { get; set; }
        public long Frequency { get; set; }
        public int NrNeurons { get; set; }
        public int MemNeuronBatchSize { get; set; }
        public int MemSynapseBatchSize { get; set; }
        public int InputLatency { get; set; }
        public int InternalLatency { get; set; }
        public int OutputLatency { get; set; }
        public int ComputeLatency { get; set; }
        public int PipelineII { get; set; }
        public int PipelineLat { get; set; }
        public int MemLatency { get; set; }
        public int SchedulerBufferSize { get; set; }
        public int NrCores { get; set; }

        // Technology
        public double MemNeuronReadEnergy { get; set; }
        public double MemNeuronWriteEnergy { get; set; }
        public double MemNeuronLeakage { get; set; }
        public double MemSynReadEnergy { get; set; }
        public double MemSynWriteEnergy { get; set; }
        public double MemSynapseLeakage { get; set; }
        public double BufferLeakage { get; set; }
        public double BufferDynamic { get; set; }
        public double CoreLeakage { get; set; }
        public double CoreDynamic { get; set; }
        public double RouterLeakage { get; set; }
        public double RouterDynamic { get; set; }
        public double ControllerLeakage { get; set; }
    }
}