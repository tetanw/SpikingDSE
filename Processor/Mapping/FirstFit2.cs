using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public class FirstFitMapper2 : Mapper
{
    record class CoreData
    {
        public CoreSpec Spec { get; set; }
        public int NrNeurons { get; set; }
        public int NrSynapses { get; set; }
        public int NrLayers { get; set; }
        public int NrFanIn { get; set; }

        public CoreData(CoreSpec spec)
        {
            Spec = spec;
            NrNeurons = 0;
            NrSynapses = 0;
            NrLayers = 0;
            NrFanIn = 0;
        }

        public bool FitsLayer(Layer layer)
        {
            return NrNeurons + layer.Size <= Spec.MaxNeurons &&
                            NrSynapses + layer.InputSize * layer.Size <= Spec.MaxSynapses &&
                            NrLayers < Spec.MaxLayers &&
                            NrFanIn + layer.InputSize <= Spec.MaxFanIn &&
                            Spec.AcceptedTypes.Contains(layer.TypeName);
        }

        public int MaximumCut(Layer layer, int neuronsToMap)
        {
            // Not a valid to core to use as target
            if (!Spec.AcceptedTypes.Contains(layer.TypeName))
                return 0;

            // If the maximum amount of layers is reached then also continue
            if (NrLayers == Spec.MaxLayers)
                return 0;

            if (NrFanIn + layer.InputSize > Spec.MaxFanIn)
                return 0;

            // What is the maximum amount of neurons that can be mapped 
            // according to neuron memory? 
            int limitedByNeuron = Spec.MaxNeurons - NrNeurons;

            // What is the maximum amount of neurons that can be mapped
            // according to synapse memory
            int freeSynapses = Spec.MaxSynapses - NrSynapses;
            int limitedBySynapse = freeSynapses / layer.InputSize;

            return Math.Min(Math.Min(limitedByNeuron, limitedBySynapse), neuronsToMap);
        }
    }

    record class LayerData
    {
        public Layer Layer { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public int Index { get; set; }
    }

    public FirstFitMapper2(HWSpec hw, SNN snn) : base(hw, snn)
    {
    }

    public override Mapping Run()
    {
        var mapping = new Mapping();
        var layers = snn.GetAllLayers();

        // Do the actual mapping
        var unmapped = new Queue<Layer>(layers);
        var sortedCores = hw.Cores.Select(c => new CoreData(c)).ToList();
        sortedCores.Sort((c1, c2) => c1.Spec.Priority < c2.Spec.Priority ? 1 : -1);
        while (unmapped.Count > 0)
        {
            var layer = unmapped.Dequeue();

            if (!layer.Splittable)
            {
                var core = sortedCores.Find(c => c.FitsLayer(layer));
                if (core != null)
                {
                    core.NrNeurons += layer.Size;
                    core.NrSynapses += layer.InputSize * layer.Size + (layer.Recurrent ? layer.Size * layer.Size : 0);
                    core.NrFanIn += layer.InputSize;
                    core.NrLayers++;
                    mapping.Mapped.Add(new MappedLayer
                    {
                        Layer = layer.Name,
                        Core = core.Spec.Name,
                        Partial = false,
                        Start = 0,
                        End = layer.Size,
                        Index = 0
                    });
                    continue;
                }
                else
                {

                    mapping.Unmapped.Add(layer.Name);
                    continue;

                }
            }
            else
            {
                Dictionary<CoreData, LayerData> splitSet = new();
                int i = 1;
                int mappedNeurons = 0;
                foreach (var c in sortedCores)
                {
                    int neuronsToMap = layer.Size - mappedNeurons;
                    int toMap = c.MaximumCut(layer, neuronsToMap);
                    if (toMap == 0)
                        continue;

                    splitSet.Add(c, new LayerData
                    {
                        Layer = layer,
                        Start = mappedNeurons,
                        End = mappedNeurons + toMap,
                        Index = i++
                    });
                    mappedNeurons += toMap;
                    if (mappedNeurons == layer.Size)
                    {
                        break;
                    }
                }
                if (mappedNeurons != layer.Size)
                {
                    // the layer is too big to be mapped even though it is splittable
                    mapping.Unmapped.Add(layer.Name);
                    continue;
                }
                int maxSplits = splitSet.Min(c => c.Key.Spec.MaxSplits);
                if (splitSet.Count >= maxSplits)
                {
                    // the layer is splitted in too many parts
                    mapping.Unmapped.Add(layer.Name);
                    continue;
                }
                Debug.Assert(splitSet.Count > 0);
                Debug.Assert(mappedNeurons == layer.Size);

                // actually assign the splits to each core
                foreach (var (c, l) in splitSet)
                {
                    mapping.Mapped.Add(new MappedLayer
                    {
                        Layer = layer.Name,
                        Core = c.Spec.Name,
                        Partial = true,
                        Start = l.Start,
                        End = l.End,
                        Index = l.Index
                    });
                    var sliceSize = l.End - l.Start;
                    c.NrNeurons += sliceSize;
                    c.NrSynapses += sliceSize * sliceSize + sliceSize * l.Layer.InputSize;
                    c.NrFanIn += layer.InputSize;
                    c.NrLayers++;
                }
            }
        }
        return mapping;
    }
}