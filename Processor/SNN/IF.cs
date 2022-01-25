using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class IFLayer : HiddenLayer2
{
    public float[] Pots;
    public float[] Readout;
    public float[,] weights;
    public float Thr;
    public float[] Alpha;

    public IFLayer(float[,] weights, float[] alpha, float threshold = 0.01f, string name = "")
    {
        this.InputSize = weights.GetLength(0);
        this.Size = weights.GetLength(1);
        this.weights = weights;
        this.Pots = new float[Size];
        this.Readout = new float[Size];
        this.Thr = threshold;
        this.Alpha = alpha;
        this.Name = name;
    }
    public override void Forward(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += weights[neuron, dst];
        }
    }

    public override IEnumerable<int> Sync()
    {
        for (int dst = 0; dst < Size; dst++)
        {
            float pot = Pots[dst];

            // Readout
            Readout[dst] = pot;

            // Leakage for next ts
            pot *= Alpha[dst];

            // Writeback
            Pots[dst] = pot;
        }

        yield break;
    }
}