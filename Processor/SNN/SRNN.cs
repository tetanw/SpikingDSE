using System.Collections.Generic;

namespace SpikingDSE;

public class SRNN : SNN
{
    public InputLayer Input;
    public ALIFLayer[] Hidden;
    public OutputLayer Output;

    public static SRNN Load(string folderPath, ISpikeSource spikeSource)
    {
        var input = new InputLayer(spikeSource, name: "i");
        var hidden = new ALIFLayer[2];
        hidden[0] = createALIFLayer(folderPath, "i", "h1");
        hidden[1] = createALIFLayer(folderPath, "h1", "h2");
        var output = createOutputLayer(folderPath);
        return new SRNN(input, hidden, output);
    }

    public SRNN(InputLayer input, ALIFLayer[] hidden, OutputLayer output)
    {
        this.Input = input;
        this.Hidden = hidden;
        this.Output = output;
        AddForward(input, hidden[0]);
        AddForward(hidden[0], hidden[1]);
        AddForward(hidden[1], output);
    }

    private static ALIFLayer createALIFLayer(string folderPath, string inputName, string name)
    {
        float[] tau_m1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_{name}.csv", headers: true);
        float[] tau_adp1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_adp_{name}.csv", headers: true);
        float[] alpha1 = tau_m1.Transform(WeigthsUtil.Exp);
        float[] rho1 = tau_adp1.Transform(WeigthsUtil.Exp);
        float[] alphaComp1 = alpha1.Transform((_, a) => 1 - a);
        var hidden = new ALIFLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_{inputName}_2_{name}.csv", headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp1)),
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_{name}_2_{name}.csv", headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp1)),
            WeigthsUtil.Read1DFloat($"{folderPath}/bias_{name}.csv", headers: true),
            alpha1,
            rho1,
            0.01f,
            name: $"{name}"
        );
        return hidden;
    }

    private static OutputLayer createOutputLayer(string folderPath)
    {
        float[] tau_m3 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_o.csv", headers: true);
        float[] alpha3 = tau_m3.Transform(WeigthsUtil.Exp);
        float[] alphaComp3 = alpha3.Transform((_, a) => 1 - a);
        var output = new OutputLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_h2_2_o.csv", headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp3)),
            alpha3,
            threshold: 0.01f,
            name: "output"
        );
        return output;
    }

    public SRNN Copy(ISpikeSource spikeSource)
    {
        var input = this.Input.Copy(spikeSource);
        var hidden = new ALIFLayer[this.Hidden.Length];
        // TODO: Test whether this code works
        for (int i = 0; i < hidden.Length; i++)
        {
            hidden[i] = this.Hidden[i].Copy();
        }
        var output = this.Output.Copy();
        var other = new SRNN(input, hidden, output);
        return other;
    }

    public int Prediction() => this.Output.Prediction();
}

public class SplittedSRNN : SNN
{
    public InputLayer Input;
    public List<List<ALIFLayer>> HiddenLayers;
    public OutputLayer Output;

    public static SplittedSRNN SplitSRNN(SRNN srnn, Mapping mapping, ISpikeSource spikeSource)
    {
        var input = srnn.Input = new InputLayer(spikeSource, "i");
        var output = srnn.Output.Copy();

        List<List<ALIFLayer>> hiddenLayers = new();
        foreach (var hidden in srnn.Hidden)
        {
            List<ALIFLayer> parts = new();
            int i = 1;
            foreach (var split in mapping.GetAllSplits(hidden.Name))
            {
                parts.Add(hidden.Slice(
                    split.Start,
                    split.End,
                    i++
                ));
            }
            hiddenLayers.Add(parts);
        }

        return new SplittedSRNN(input, hiddenLayers, output);
    }

    public SplittedSRNN(InputLayer input, List<List<ALIFLayer>> hiddenLayers, OutputLayer output)
    {
        this.Input = input;
        this.HiddenLayers = hiddenLayers;
        this.Output = output;

        // Register connections to snn
        RegisterForwards();
        foreach (var hidden in hiddenLayers)
        {
            GroupSiblings(hidden);
        }
    }

    public SplittedSRNN Copy(ISpikeSource spikeSource)
    {
        var input = this.Input.Copy(spikeSource);
        var output = this.Output.Copy();
        List<List<ALIFLayer>> hiddenLayers = new();
        foreach (var layer in this.HiddenLayers)
        {
            List<ALIFLayer> parts = new();
            foreach (var part in layer)
            {
                parts.Add(part.Copy());
            }
            hiddenLayers.Add(parts);
        }

        return new SplittedSRNN(input, hiddenLayers, output);
    }

    private void RegisterForwards()
    {
        // input to first hidden layer
        foreach (var hidden in HiddenLayers[0])
        {
            AddForward(Input, hidden);
        }

        // Hidden layers to each other
        for (int i = 0; i < HiddenLayers.Count - 1; i++)
        {
            var cur = HiddenLayers[i];
            var next = HiddenLayers[i + 1];

            foreach (var src in cur)
            {
                foreach (var dst in next)
                {
                    AddForward(src, dst);
                }
            }
        }

        // last hidden layer to output
        foreach (var hidden in HiddenLayers[HiddenLayers.Count - 1])
        {
            AddForward(hidden, Output);
        }
    }

    public int Prediction() => this.Output.Prediction();
}