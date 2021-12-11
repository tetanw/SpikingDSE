using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class RLIFLayer2 : HiddenLayer2
{
    public readonly float[,] InWeights;
    public readonly float[,] RecWeights;
    public float[] Pots;
    public float Leakage;
    public bool[] Spiked;
    public ResetMode ResetMode;
    public float Thr;
    public float[] Readout;

    public RLIFLayer2(float[,] inWeights, float[,] recWeights, string name)
    {
        this.InputSize = inWeights.GetLength(0) + recWeights.GetLength(0);
        this.Size = inWeights.GetLength(1);
        this.Pots = new float[Size];
        this.Readout = new float[Size];
        this.Spiked = new bool[Size];
        this.InWeights = inWeights;
        this.RecWeights = recWeights;
        this.Name = name;
        this.InputSize = inWeights.GetLength(0);
        this.Size = inWeights.GetLength(1);
    }

    public override void Forward(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += InWeights[neuron, dst];
        }
    }

    public void Feedback(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += RecWeights[neuron, dst];
        }
    }

    public override IEnumerable<int> Sync()
    {
        for (int dst = 0; dst < Size; dst++)
        {
            float pot = Pots[dst];

            // Readout
            Readout[dst] = pot;

            // Reset
            if (Spiked[dst])
                pot -= Thr;

            // Threshold
            if (pot >= Thr)
            {
                Spiked[dst] = true;
                yield return dst;
            }
            else
            {
                Spiked[dst] = false;
            }

            // Leakage for next ts
            pot *= Leakage;

            // Writeback
            Pots[dst] = pot;
        }
    }
}