using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class RLIFLayer : HiddenLayer
{
    public readonly float[,] InWeights;
    public readonly float[,] RecWeights;
    public float[] Pots;
    public float Leakage;
    public bool[,] Spiked;
    public ResetMode ResetMode;
    public float Thr;

    public RLIFLayer(float[,] inWeights, float[,] recWeights, string name)
    {
        this.InputSize = inWeights.GetLength(0) + recWeights.GetLength(0);
        this.Size = inWeights.GetLength(1);
        this.Pots = new float[Size];
        this.InWeights = inWeights;
        this.RecWeights = recWeights;
        this.Name = name;
        this.InputSize = inWeights.GetLength(0);
        this.Size = inWeights.GetLength(1);
    }

    public override void Leak()
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] = Pots[dst] * Leakage;
        }
    }

    public override void Integrate(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += InWeights[neuron, dst];
        }
    }

    public void IntegrateFeedback(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += RecWeights[neuron, dst];
        }
    }

    public override void ApplyThreshold(int neuron)
    {
        if (ResetMode == ResetMode.Zero)
            Pots[neuron] = 0;
        else if (ResetMode == ResetMode.Subtract)
            Pots[neuron] -= Thr;
        else
            throw new Exception("Unknown reset behaviour");
    }

    public override IEnumerable<int> Threshold()
    {
        for (int dst = 0; dst < Size; dst++)
        {
            if (Pots[dst] >= Thr)
                yield return dst;
        }
    }
}