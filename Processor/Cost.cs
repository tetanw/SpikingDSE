namespace SpikingDSE
{
    public class CostConfig
    {
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