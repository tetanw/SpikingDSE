using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class IFLayer : HiddenLayer
{
    public float[] Pots;
    public float[,] weights;
    public float Thr;
    public float leakage;
    public bool[] spiked;
    private bool refractory;
    public ResetMode ResetMode;

    public IFLayer(float[,] weights, float threshold = 30, float leakage = 0, bool refractory = true, ResetMode resetMode = ResetMode.Zero, string name = "")
    {
        this.InputSize = weights.GetLength(0);
        this.Size = weights.GetLength(1);
        this.weights = weights;
        this.Pots = new float[Size];
        this.spiked = new bool[Size];
        this.Thr = threshold;
        this.leakage = leakage;
        this.refractory = refractory;
        this.ResetMode = resetMode;
        this.Name = name;
    }

    public override void Leak()
    {
        for (int dst = 0; dst < Size; dst++)
        {
            if (spiked[dst])
            {
                spiked[dst] = false;
            }
            Pots[dst] = Pots[dst] * leakage;
        }
    }

    public override void Integrate(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += weights[neuron, dst];
        }
    }

    public override IEnumerable<int> Threshold()
    {
        for (int dst = 0; dst < Size; dst++)
        {
            if (spiked[dst] && refractory)
                continue;

            if (Pots[dst] >= Thr)
            {
                if (ResetMode == ResetMode.Zero)
                    Pots[dst] = 0;
                else if (ResetMode == ResetMode.Subtract)
                    Pots[dst] -= Thr;
                else
                    throw new Exception("Unknown reset behaviour");
                spiked[dst] = true;
                yield return dst;
            }
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
}