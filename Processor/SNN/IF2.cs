using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class IFLayer2 : HiddenLayer2
{
    public float[] Pots;
    public float[] Readout;
    public float[,] weights;
    public float Thr;
    public float leakage;
    private bool refractory;
    public ResetMode ResetMode;

    public IFLayer2(float[,] weights, float threshold = 30, float leakage = 0, bool refractory = true, ResetMode resetMode = ResetMode.Zero, string name = "")
    {
        this.InputSize = weights.GetLength(0);
        this.Size = weights.GetLength(1);
        this.weights = weights;
        this.Pots = new float[Size];
        this.Readout = new float[Size];
        this.Thr = threshold;
        this.leakage = leakage;
        this.refractory = refractory;
        this.ResetMode = resetMode;
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
            pot *= leakage;

            // Writeback
            Pots[dst] = pot;
        }

        yield break;
    }
}