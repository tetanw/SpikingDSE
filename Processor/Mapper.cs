using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpikingDSE
{
    public abstract class Mapper
    {
        protected SNN snn;
        protected HWConfig hw;
        public int[] coreTable;
        protected bool[,] targetCoreTable;

        public Mapper(SNN snn, HWConfig hw, string name)
        {
            this.Name = name;
            this.snn = snn;
            this.hw = hw;

            coreTable = new int[snn.NrNeurons];
            for (int i = 0; i < snn.NrNeurons; i++)
            {
                coreTable[i] = -1;
            }

        }

        public int GetCore(int neuronID)
        {
            return coreTable[neuronID];
        }

        protected void SetMapping(int neuronID, int coreID)
        {
            coreTable[neuronID] = coreID;
        }

        protected void Construct()
        {
            targetCoreTable = new bool[snn.NrNeurons, hw.NrPEs];
            for (int i = 0; i < snn.NrNeurons; i++)
            {
                var inputLayer = snn.GetLayerByNeuron(i);
                var outputLayer = snn.GetNextLayer(inputLayer);
                if (outputLayer == null)
                    continue;

                for (int j = outputLayer.FirstNeuronID; j <= outputLayer.LastNeuronID; j++)
                {
                    int targetCore = GetCore(j);
                    targetCoreTable[i, targetCore] = true;
                }
            }
        }

        public bool CoreHasNeuron(int coreID, int neuronID)
        {
            return coreTable[neuronID] == coreID;
        }

        public List<int> GetOutputCores(int neuronID)
        {
            List<int> outputCores = new List<int>();
            for (int core = 0; core < hw.NrPEs; core++)
            {
                if (targetCoreTable[neuronID, core])
                    outputCores.Add(core);
            }

            if (snn.isOutputNeuron(neuronID))
            {
                outputCores.Add(-1);
            }

            return outputCores;
        }

        public List<SpikeRoute> GetAllSpikeRoutes(List<int> spikes)
        {
            var routes = new List<SpikeRoute>();
            foreach (var spike in spikes)
            {
                routes.Add(new SpikeRoute
                {
                    NeuronID = spike,
                    CoreFrom = snn.isInputNeuron(spike) ? -1 : GetCore(spike),
                    CoreTos = GetOutputCores(spike).ToArray()
                });
            }
            return routes;
        }

        public abstract void ConstructMapping();

        public string Name
        {
            get; protected set;
        }
    }

    public class RoundRobinMapper : Mapper
    {
        public RoundRobinMapper(SNN snn, HWConfig hw) : base(snn, hw, "RoundRobin")
        {
        }

        public override void ConstructMapping()
        {
            for (int neuronID = 0; neuronID < snn.NrNeurons; neuronID++)
            {
                int coreID = neuronID % hw.NrPEs;
                SetMapping(neuronID, coreID);
            }
            Construct();
        }
    }

    public class FirstFitMapper : Mapper
    {
        public FirstFitMapper(SNN snn, HWConfig hw) : base(snn, hw, "FirstFit")
        {
        }

        public override void ConstructMapping()
        {
            int[] nrNeurons = new int[hw.NrPEs];

            // find a suitable core for each layer
            for (int i = 0; i < snn.Layers.Count; i++)
            {
                var layer = snn.Layers[i];

                // we check whether the layer fits in the core
                // starting from the first core
                bool layerFound = false;
                for (int j = 0; j < hw.NrPEs; j++)
                {
                    if (layer.Size + nrNeurons[j] <= hw.NrNeurons)
                    {
                        // if it fits then we can map all the neurons in the layer to
                        // the core that fits
                        for (int k = layer.FirstNeuronID; k <= layer.LastNeuronID; k++)
                        {
                            SetMapping(k, j);
                        }

                        layerFound = true;
                        break;
                    }
                }

                // in case the layer fits nowhere we crash. This is not supported
                if (!layerFound)
                {
                    throw new Exception("No layer found!");
                }
            }
            Construct();
        }
    }

    public struct SpikeRoute
    {
        [JsonPropertyName("ID")]
        public int NeuronID { get; set; } // the neuron that spiked
        [JsonPropertyName("Src")]
        public int CoreFrom { get; set; } // the core where the neuron spiked on
        [JsonPropertyName("Dest")]
        public int[] CoreTos { get; set; } // the cores that the neuron travels to

        public override string ToString()
        {
            return $"[{NeuronID}] {CoreFrom} -> {{{string.Join(", ", CoreTos)}}}";
        }
    }

    public enum MappingStrategy
    {
        NeuronRoundRobin,
        LayerFirstFit
    }
}