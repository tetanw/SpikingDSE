using System.Collections.Generic;
using System;

namespace SpikingDSE
{
    public interface NeuronLocator<T>
    {
        public T Locate(int neuron);
    }

    public struct MeshCoord
    {
        public readonly int x, y;

        public MeshCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public void Deconstruct(out int x, out int y)
        {
            x = this.x;
            y = this.y;
        }
    }

    class LayerMapping
    {
        public Layer Layer;
        public MeshCoord Coordinates;
    }

    public class LayerMeshLocator : NeuronLocator<MeshCoord>
    {
        private List<LayerMapping> mappings;

        public void AddMapping(Layer layer, MeshCoord coordinates)
        {
            if (mappings == null)
                mappings = new List<LayerMapping>();

            mappings.Add(new LayerMapping { Layer = layer, Coordinates = coordinates });
        }

        public MeshCoord Locate(int neuron)
        {
            LayerMapping matchingMapping = null;
            foreach (var mapping in mappings)
            {
                if (neuron >= mapping.Layer.startID && neuron < mapping.Layer.endID)
                {
                    matchingMapping = mapping;
                }
            }

            if (matchingMapping == null)
            {
                throw new Exception($"Could not mapping neuron with ID: {neuron}");
            }
            else
            {
                return matchingMapping.Coordinates;
            }
        }
    }
}