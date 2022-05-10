using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class OutputLayer : HiddenLayer
{
    public int TS = 0;
    public float[] ReadoutArr;
    public float[] Pots;
    public float[] Output;
    public float[,] Weights;
    public float Thr;
    public float[] Alpha;

    public OutputLayer(float[,] weights, float[] alpha, float threshold = 0.01f, string name = "")
    {
        InputSize = weights.GetLength(0);
        Size = weights.GetLength(1);
        Weights = weights;
        Pots = new float[Size];
        ReadoutArr = new float[Size];
        Output = new float[Size];
        Thr = threshold;
        Alpha = alpha;
        Name = name;
        TypeName = "output";
        Splittable = false;
        Recurrent = false;
        NrSynapses = Size * InputSize;
    }

    public override void Forward(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += Weights[neuron, dst];
        }
    }

    public override bool Sync(int dst)
    {
        float pot = Pots[dst];

        // Readout
        ReadoutArr[dst] = pot;

        // Leakage for next ts
        pot *= Alpha[dst]; // *

        // Writeback
        Pots[dst] = pot;

        return false;
    }

    public override void FinishSync()
    {
        UpdateOutput();
        TS++;
    }

    private void UpdateOutput()
    {
        float[] softmax = Softmax(ReadoutArr);
        for (int i = 0; i < Size; i++)
        {
            Output[i] += softmax[i];
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

    public override Layer Copy()
    {
        return new OutputLayer(
            Weights,
            Alpha,
            name: Name
        );
    }

    public override Layer Slice(int start, int end, int partNr)
    {
        throw new Exception("Can not slice output layer");
    }

    public override int Offset() => 0;

    public override void Feedback(int neuron) { }

    public override float[] Readout() => ReadoutArr;
}