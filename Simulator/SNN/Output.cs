using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class OutputLayer : HiddenLayer
{
    public double[] Pots;
    public double[] Output;
    public float[,] Weights;
    public float Thr;
    public float[] Alpha;
    public float[] Inputs;

    public OutputLayer(float[,] weights, float[] alpha, float threshold = 0.01f, string name = "")
    {
        InputSize = weights.GetLength(0);
        Size = weights.GetLength(1);
        Weights = weights;
        Pots = new double[Size];
        Inputs = new float[Size];
        Output = new double[Size];
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
            Inputs[dst] += Weights[neuron, dst];
        }
    }

    public override bool Sync(int dst)
    {
        // Leakage for next ts
        Pots[dst] = Alpha[dst] * Pots[dst] + Inputs[dst]; // *

        // Reset inputs
        Inputs[dst] = 0.0f;

        return false;
    }

    public override void FinishSync()
    {
        UpdateOutput();
        base.FinishSync();
    }

    private void UpdateOutput()
    {
        double[] softmax = Softmax(Pots);
        for (int i = 0; i < Size; i++)
        {
            Output[i] += softmax[i];
        }
    }

    private static double[] Softmax(double[] vector)
    {
        var res = new double[vector.Length];
        var sum = 0.0;
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

    public override float[] Readout() => Pots.Select(p => (float) p).ToArray();
}