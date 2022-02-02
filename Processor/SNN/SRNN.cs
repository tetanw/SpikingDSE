using System.Collections.Generic;

namespace SpikingDSE;

public class SRNN : SNN
{
    public InputLayer input;
    public ALIFLayer[] hidden;
    public OutputIFLayer output;

    public static SRNN Load(string folderPath, ISpikeSource spikeSource)
    {
        var input = new InputLayer(spikeSource, name: "i");
        var hidden = new ALIFLayer[2];
        hidden[0] = createALIFLayer(folderPath, "i", "h1");
        hidden[1] = createALIFLayer(folderPath, "h1", "h2");
        var output = createOutputLayer(folderPath);
        return new SRNN(input, hidden, output);
    }

    public SRNN(InputLayer input, ALIFLayer[] hidden, OutputIFLayer output)
    {
        this.input = input;
        this.hidden = hidden;
        this.output = output;
        AddConnection(input, hidden[0]);
        AddConnection(hidden[0], hidden[1]);
        AddConnection(hidden[1], output);
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

    private static OutputIFLayer createOutputLayer(string folderPath)
    {
        float[] tau_m3 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_o.csv", headers: true);
        float[] alpha3 = tau_m3.Transform(WeigthsUtil.Exp);
        float[] alphaComp3 = alpha3.Transform((_, a) => 1 - a);
        var output = new OutputIFLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_h2_2_o.csv", headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp3)),
            alpha3,
            threshold: 0.01f,
            name: "output"
        );
        return output;
    }

    public SRNN Copy(ISpikeSource spikeSource)
    {
        var input = new InputLayer(spikeSource, "i");
        var hidden = new ALIFLayer[2];
        hidden[0] = this.hidden[0].Copy();
        hidden[1] = this.hidden[1].Copy();
        var output = this.output.Copy();
        var other = new SRNN(input, hidden, output);
        return other;
    }

    public int Prediction() => this.output.Prediction();
}

public class SplittedSRNN : SNN
{
    private InputLayer input;
    private List<List<HiddenLayer>> hiddenLayers;
    private OutputIFLayer output;

    public SplittedSRNN(SRNN srnn, ISpikeSource spikeSource, int chunkSize)
    {
        this.input = srnn.input = new InputLayer(spikeSource, "i");
        hiddenLayers = new();
        foreach (var hidden in srnn.hidden)
        {
            List<HiddenLayer> parts = new List<HiddenLayer>();
            parts.AddRange(hidden.Split(chunkSize));
            hiddenLayers.Add(parts);
        }
        this.output = srnn.output.Copy();

        foreach (var hidden in hiddenLayers[0])
        {
            AddConnection(input, hidden);
        }

        for (int i = 0; i < hiddenLayers.Count - 1; i++)
        {
            var cur = hiddenLayers[i];
            var next = hiddenLayers[i + 1];
        
            foreach (var src in cur)
            {
                foreach (var dst in next)
                {
                    AddConnection(src, dst);
                }
            }
        }

        foreach (var hidden in hiddenLayers[1])
        {
            AddConnection(hidden, output);
        }
    }

    public int Prediction() => this.output.Prediction();
}