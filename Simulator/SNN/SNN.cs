using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public class SNN
{
    private readonly List<List<Layer>> layerParts;
    public Dictionary<Layer, HashSet<Layer>> outputs = new();
    public Dictionary<Layer, HashSet<Layer>> inputs = new();
    public Dictionary<Layer, List<Layer>> siblings = new();
    public HashSet<Layer> layers = new();
    private readonly InputLayer input;
    private readonly OutputLayer output;

    public SNN(List<Layer> layers)
    {
        for (int i = 0; i < layers.Count - 1; i++)
        {
            var current = layers[i];
            var next = layers[i + 1];
            AddForward(current, next);
        }
        input = layers.First() as InputLayer;
        output = layers.Last() as OutputLayer;
    }

    public SNN(List<List<Layer>> layerParts)
    {
        for (int i = 0; i < layerParts.Count - 1; i++)
        {
            var current = layerParts[i];
            var next = layerParts[i + 1];

            foreach (var source in current)
            {
                foreach (var dest in next)
                {
                    AddForward(source, dest);
                }
            }
        }

        for (int i = 1; i < layerParts.Count - 1; i++)
        {
            var layer = layerParts[i];
            GroupSiblings(layer);
        }

        input = layerParts.First().First() as InputLayer;
        output = layerParts.Last().First() as OutputLayer;
        this.layerParts = layerParts;
    }

    private static ALIFLayer CreateALIFLayer(string path, Dictionary<string, JsonElement> layer)
    {
        string tauMPath = path + layer["TauM"].GetString();
        string tauAdpPath = path + layer["TauAdp"].GetString();
        string inWeightsPath = path + layer["InWeights"].GetString();
        string recWeightsPath = path + layer["RecWeights"].GetString();
        string biasPath = path + layer["Bias"].GetString();
        float vth = (float)layer["Vth"].GetDouble();
        float beta = (float)layer["Beta"].GetDouble();

        float[] tau_m = WeigthsUtil.Read1DFloat(tauMPath, headers: true);
        float[] tau_adp = WeigthsUtil.Read1DFloat(tauAdpPath, headers: true);
        float[] alpha = tau_m.Transform(WeigthsUtil.Exp);
        float[] rho = tau_adp.Transform(WeigthsUtil.Exp);
        float[] alphaComp = alpha.Transform((_, a) => 1 - a);
        var hidden = new ALIFLayer(
            WeigthsUtil.Read2DFloat(inWeightsPath, headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp)),
            WeigthsUtil.Read2DFloat(recWeightsPath, headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp)),
            WeigthsUtil.Read1DFloat(biasPath, headers: true),
            alpha,
            rho,
            beta,
            vth,
            name: layer["Name"].GetString()
        );
        return hidden;
    }

    private static ALIFQLayer CreateALIFQLayer(string path, Dictionary<string, JsonElement> layer)
    {
        string tauMPath = path + layer["TauM"].GetString();
        string tauAdpPath = path + layer["TauAdp"].GetString();
        string inWeightsPath = path + layer["InWeights"].GetString();
        string recWeightsPath = path + layer["RecWeights"].GetString();
        string biasPath = path + layer["Bias"].GetString();
        float vth = (float)layer["Vth"].GetDouble();
        float beta = (float)layer["Beta"].GetDouble();
        float scale = (float)layer["Scale"].GetDouble();

        var scale1D = (int i, float v) => (long)(v * scale);
        var scale2D = (int x, int y, float v) => (long)(v * scale);
        float[] tau_m = WeigthsUtil.Read1DFloat(tauMPath, headers: true);
        float[] tau_adp = WeigthsUtil.Read1DFloat(tauAdpPath, headers: true);
        float[] alpha = tau_m.Transform(WeigthsUtil.Exp);
        float[] rho = tau_adp.Transform(WeigthsUtil.Exp);
        float[] alphaComp = alpha.Transform((_, a) => 1 - a);
        int vthQ = (int)(vth * scale);
        int betaQ = (int)(beta * scale);
        var hidden = new ALIFQLayer(
            (int)scale,
            WeigthsUtil.Read2DFloat(inWeightsPath, headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp)).Transform(scale2D),
            WeigthsUtil.Read2DFloat(recWeightsPath, headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp)).Transform(scale2D),
            WeigthsUtil.Read1DFloat(biasPath, headers: true).Transform(scale1D),
            alpha.Transform(scale1D),
            rho.Transform(scale1D),
            vthQ,
            betaQ,
            name: layer["Name"].GetString()
        );
        return hidden;
    }

    private static OutputLayer CreateOutputLayer(string path, Dictionary<string, JsonElement> layer)
    {
        string tauMPath = path + layer["TauM"].GetString();
        string inWeightsPath = path + layer["InWeights"].GetString();
        float vth = (float) layer["Vth"].GetDouble();

        float[] tau_m = WeigthsUtil.Read1DFloat(tauMPath, headers: true);
        float[] alpha = tau_m.Transform(WeigthsUtil.Exp);
        float[] alphaComp = alpha.Transform((_, a) => 1 - a);
        var output = new OutputLayer(
            WeigthsUtil.Read2DFloat(inWeightsPath, headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp)),
            alpha,
            threshold: vth,
            name: layer["Name"].GetString()
        );
        return output;
    }

    public static SNN Load(string path)
    {
        var snnFile = JsonSerializer.Deserialize<SNNFile>(File.ReadAllText(path));
        var layers = new List<Layer>();
        foreach (var layer in snnFile.Layers)
        {
            string type = layer["Type"].GetString();
            if (type == "input")
            {
                int size = layer["Size"].GetInt32();
                string name = layer["Name"].GetString();
                layers.Add(new InputLayer(size, name));
            }
            else if (type == "ALIF")
            {
                layers.Add(CreateALIFLayer(snnFile.BasePath, layer));
            }
            else if (type == "output")
            {
                layers.Add(CreateOutputLayer(snnFile.BasePath, layer));
            }
            else if (type == "ALIFQ")
            {
                layers.Add(CreateALIFQLayer(snnFile.BasePath, layer));
            }
            else
            {
                throw new Exception($"Unknown layer type: {type}");
            }
        }

        return new SNN(layers);
    }

    public static SNN SplitSNN(SNN snn, Mapping mapping)
    {
        var input = snn.GetInputLayer().Copy();
        var output = snn.GetOutputLayer().Copy();

        List<List<Layer>> hiddenLayers = new()
        {
            new List<Layer>() { input }
        };
        var hiddens = snn.layers.Where((l, i) => i != 0 && i != snn.layers.Count - 1).ToList();
        foreach (var hidden in hiddens)
        {
            List<Layer> parts = new();
            int partNr = 1;

            foreach (var split in mapping.GetAllSplits(hidden.Name))
            {
                if (split.Partial)
                {
                    parts.Add(hidden.Slice(split.Start, split.End, partNr++));
                }
                else
                {
                    parts.Add(hidden.Copy());
                }
            }
            hiddenLayers.Add(parts);
        }
        hiddenLayers.Add(new List<Layer>() { output });

        return new SNN(hiddenLayers);
    }

    public Layer FindLayer(string name)
    {
        return layers.FirstOrDefault(l => l.Name == name);
    }

    public InputLayer GetInputLayer()
    {
        return input;
    }

    public OutputLayer GetOutputLayer()
    {
        return output;
    }

    public void AddForward(Layer from, Layer to)
    {
        if (outputs.TryGetValue(from, out HashSet<Layer> l))
        {
            l.Add(to);
        }
        else
        {
            outputs[from] = new() { to };
        }

        if (inputs.TryGetValue(to, out l))
        {
            l.Add(from);
        }
        else
        {
            inputs[to] = new() { from };
        }

        layers.Add(from);
        layers.Add(to);
    }

    public void GroupSiblings(IEnumerable<Layer> layers)
    {
        var layerList = layers.ToList();
        foreach (var layer in layerList)
        {
            siblings[layer] = layerList;
        }
    }

    public IEnumerable<Layer> GetAllLayers()
    {
        return layers;
    }

    public Layer GetSourceLayer(Layer layer)
    {
        return GetSourceLayers(layer)?.First();
    }

    public Layer GetDestLayer(Layer layer)
    {
        return GetDestLayers(layer)?.First();
    }

    public IEnumerable<Layer> GetDestLayers(Layer layer)
    {
        outputs.TryGetValue(layer, out HashSet<Layer> outVal);
        return outVal;
    }

    public IEnumerable<Layer> GetSourceLayers(Layer layer)
    {
        inputs.TryGetValue(layer, out HashSet<Layer> outVal);
        return outVal;
    }

    public IEnumerable<Layer> GetSiblingLayers(Layer layer)
    {
        siblings.TryGetValue(layer, out List<Layer> outVal);
        return outVal ?? Enumerable.Empty<Layer>();
    }

    public SNN Copy()
    {
        if (layerParts == null)
            throw new Exception("Can not copy");

        var newLayers = new List<List<Layer>>();

        foreach (var layer in layerParts)
        {
            var newLayer = new List<Layer>();
            foreach (var part in layer)
            {
                newLayer.Add(part.Copy());
            }
            newLayers.Add(newLayer);
        }

        return new SNN(newLayers);
    }
}

public class SNNFile
{
    public List<Dictionary<string, JsonElement>> Layers { get; set; }
    public string BasePath { get; set; }
}