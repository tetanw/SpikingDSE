using System.Collections.Generic;

namespace SpikingDSE
{

    public class SNN
    {
        public List<Layer> Layers { get; private set; }
        public int NrNeurons { get; private set; }

        public SNN()
        {
            this.Layers = new List<Layer>();
        }

        public void AddLayer(string name, int size, bool isInput, bool isOutput)
        {
            var layer = new Layer()
            {
                Name = name,
                Size = size,
                FirstNeuronID = NrNeurons,
                Input = isInput,
                Output = isOutput,
                LastNeuronID = NrNeurons + size - 1
            };
            Layers.Add(layer);
            NrNeurons += size;
        }

        public Layer GetLayerByNeuron(int neuronID)
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                if (neuronID >= Layers[i].FirstNeuronID && neuronID <= Layers[i].LastNeuronID)
                {
                    return Layers[i];
                }
            }

            return null;
        }

        public Layer GetNextLayer(Layer prevLayer)
        {
            int prevLayerIndex = Layers.FindIndex(x => x == prevLayer);
            if (prevLayerIndex == Layers.Count - 1)
            {
                return null;
            }
            else
            {
                return Layers[prevLayerIndex + 1];
            }
        }

        public bool isInputNeuron(int neuronID)
        {
            return GetLayerByNeuron(neuronID).Input;
        }

        public bool isOutputNeuron(int neuronID)
        {
            return GetLayerByNeuron(neuronID).Output;
        }
    }

    public class Layer
    {
        public int FirstNeuronID { get; set; }
        public int LastNeuronID { get; set; }
        public string Name { get; set; }
        public bool Input { get; set; }
        public bool Output { get; set; }
        public int Size { get; set; }
    }

    public class SNNConfig
    {
        public LayerConfig[] Layers { get; set; }
    }

    public class LayerConfig
    {
        public string Name { get; set; }
        public int Size { get; set; }
        public bool Input { get; set; }
        public bool Output { get; set; }
        public string Path { get; set; }
    }
}