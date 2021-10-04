using System;

namespace SpikingDSE
{
    public class CoreReporter : IReporter
    {
        private PEConfig config;
        private string name;

        public int nrInputSpikes = 0, nrOutputSpikes = 0;
        public int nrNeuronReads = 0, nrSynapseReads = 0;
        public int nrNeuronWrites = 0, nrSynapseWrites = 0;

        public CoreReporter(PEConfig config, string name)
        {
            this.config = config;
            this.name = name;
        }

        public void Report(Event objEv)
        {
            if (objEv is StartReceiving)
            {
                nrInputSpikes++;
            }
            else if (objEv is DoneSending)
            {
                nrOutputSpikes++;
            }
            else if (objEv is DoneComputing)
            {
                nrNeuronWrites += config.MaxSynapses / config.MemNeuron.BatchSize;
                nrNeuronReads += config.MaxSynapses / config.MemNeuron.BatchSize;
                nrSynapseWrites += config.MaxSynapses / config.MemSynapse.BatchSize;
                nrSynapseReads += config.MaxSynapses / config.MemSynapse.BatchSize;
            }
        }

        private void PrintReport(long finishCycles)
        {
            double finishTime = (double)finishCycles / config.Frequency;

            double staticNeuronMem = config.MemNeuron.Leakage * finishTime;
            double dynamicNeuronMem = config.MemNeuron.ReadEnergy * nrNeuronReads + config.MemNeuron.WriteEnergy * nrNeuronWrites;
            double staticSynapseMem = config.MemSynapse.Leakage * finishTime;
            double dynamicSynapseMem = config.MemSynapse.ReadEnergy * nrSynapseReads + config.MemSynapse.WriteEnergy * nrSynapseWrites;
            double staticController = config.Controller.Leakage * finishTime;
            double dynamicController = config.Controller.EnergyPerEvent * nrInputSpikes;
            double energyTotal = staticNeuronMem + dynamicNeuronMem + staticSynapseMem + dynamicSynapseMem + staticController + dynamicController;
            double powerTotal = energyTotal / finishTime;

            Console.WriteLine($"Results ({name}):");
            Console.WriteLine($"  End time: {finishCycles:#,0} cycles ({Measurements.GetPrefix(finishTime)}s)");
            Console.WriteLine($"  Spikes:");
            Console.WriteLine($"    Input: {nrInputSpikes:#,0} spikes");
            Console.WriteLine($"    Output: {nrOutputSpikes:#,0} spikes");
            Console.WriteLine($"  Mem:");
            Console.WriteLine($"    Neuron Mem:");
            Console.WriteLine($"      Reads: {nrNeuronReads:#,0}");
            Console.WriteLine($"      Writes: {nrNeuronWrites:#,0}");
            Console.WriteLine($"    Synapse Mem:");
            Console.WriteLine($"      Reads: {nrSynapseReads:#,0}");
            Console.WriteLine($"      Writes: {nrSynapseWrites:#,0}");
            Console.WriteLine($"  Energy:");
            Console.WriteLine($"    Leakge neuron:      {Measurements.GetPrefix(staticNeuronMem)}J");
            Console.WriteLine($"    Dynamic neuron:     {Measurements.GetPrefix(dynamicNeuronMem)}J");
            Console.WriteLine($"    Leakage synapse:    {Measurements.GetPrefix(staticSynapseMem)}J");
            Console.WriteLine($"    Dynamic synapse:    {Measurements.GetPrefix(dynamicSynapseMem)}J");
            Console.WriteLine($"    Leakage controller: {Measurements.GetPrefix(staticController)}J");
            Console.WriteLine($"    Dynamic controller: {Measurements.GetPrefix(dynamicController)}J");
            Console.WriteLine($"    Energy:             {Measurements.GetPrefix(energyTotal)}J");
            Console.WriteLine($"    Power:              {Measurements.GetPrefix(powerTotal)}W");
            // Real power from ODIN is 1092 uW
        }

        public void Start()
        {

        }

        public void End(long time)
        {
            PrintReport(time);
        }
    }
}