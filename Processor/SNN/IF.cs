using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class OutputIFLayer : HiddenLayer
{
    public int TS = 0;
    public float[] Readout;
    public float[] Pots;
    public float[] Output;
    public float[,] Weights;
    public float Thr;
    public float[] Alpha;

    public OutputIFLayer(float[,] weights, float[] alpha, float threshold = 0.01f, string name = "")
    {
        this.InputSize = weights.GetLength(0);
        this.Size = weights.GetLength(1);
        this.Weights = weights;
        this.Pots = new float[Size];
        this.Readout = new float[Size];
        this.Output = new float[Size];
        this.Thr = threshold;
        this.Alpha = alpha;
        this.Name = name;
    }
    public override void Forward(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += Weights[neuron, dst];
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

        ProcessReadout(Readout);
        TS++;

        yield break;
    }

    private void ProcessReadout(float[] pots)
    {
        if (TS > 0)
        {
            float[] softmax = Softmax(pots);
            for (int i = 0; i < Size; i++)
            {
                Output[i] += softmax[i];
            }
        }

    }

    private static float[] Softmax(float[] vector)
    {
        float[] res = new float[vector.Length];
        float sum = 0.0f;
        for (int i = 0; i < vector.Length; i++)
        {
            res[i] = (float)Math.Exp(vector[i]);
            sum += res[i];
        }
        for (int i = 0; i < vector.Length; i++)
        {
            res[i] = res[i] / sum;
        }
        return res;
    }

    public int Prediction()
    {
        return Output.ToList().IndexOf(Output.Max());
    }

    public OutputIFLayer Copy()
    {
        return new OutputIFLayer(
            this.Weights,
            this.Alpha
        );
    }
}