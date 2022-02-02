using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class ALIFLayer : HiddenLayer
{
    public float[,] InWeights;
    public float[,] RecWeights;
    public float[] Pots;
    public bool[] Spiked;
    public float VTh;
    public float[] Readout;
    public float Beta;
    public float[] Bias;
    public float[] AdaptThr;
    public float[] Alpha;
    public float[] Rho;
    private int TS;

    public ALIFLayer(float[,] inWeights, float[,] recWeights, float[] bias, float[] alpha, float[] rho, float VTh, string name)
    {
        this.InputSize = inWeights.GetLength(0);
        this.Size = inWeights.GetLength(1);
        this.Pots = new float[Size];
        this.Readout = new float[Size];
        this.Spiked = new bool[Size];
        this.InWeights = inWeights;
        this.RecWeights = recWeights;
        this.Name = name;
        this.AdaptThr = new float[Size];
        Array.Fill(AdaptThr, VTh);
        this.Bias = bias;
        this.Alpha = alpha;
        this.Rho = rho;
        this.VTh = VTh;
        this.Beta = 1.8f;
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

            // Adapt
            AdaptThr[dst] = AdaptThr[dst] * Rho[dst];
            if (Spiked[dst])
            {
                AdaptThr[dst] += (1 - Rho[dst]);
            }

            // Reset potential
            float resetPot = Beta * AdaptThr[dst] + VTh;

            // Reset
            if (Spiked[dst])
                pot -= resetPot;

            // Readout
            Readout[dst] = pot;

            // Threshold
            float thrPot = resetPot - Bias[dst];
            if (pot >= thrPot)
            {
                Spiked[dst] = true;
                yield return dst;
            }
            else
            {
                Spiked[dst] = false;
            }

            // Leakage for next ts
            pot *= Alpha[dst];

            // Writeback
            Pots[dst] = pot;
        }
        TS++;
    }

    public ALIFLayer Copy()
    {
        return new ALIFLayer(this.InWeights, this.RecWeights, this.Bias, this.Alpha, this.Rho, this.VTh, this.Name);
    }

    public ALIFLayer[] Split(int chunkSize)
    {
        // TODO: Actually deal with chunk size
        var p1 = new ALIFLayer(
            WeigthsUtil.Slice(this.InWeights, 0, 0, 64, this.InputSize),
            WeigthsUtil.Slice(this.RecWeights, 0, 0, 64, 64),
            WeigthsUtil.Slice(this.Bias, 0, 64),
            WeigthsUtil.Slice(this.Alpha, 0, 64),
            WeigthsUtil.Slice(this.Rho, 0, 64),
            0.01f,
            $"{this.Name}-1"
        );

        var p2 = new ALIFLayer(
            WeigthsUtil.Slice(this.InWeights, 64, 0, 64, this.InputSize),
            WeigthsUtil.Slice(this.RecWeights, 64, 0, 64, 64),
            WeigthsUtil.Slice(this.Bias, 64, 64),
            WeigthsUtil.Slice(this.Alpha, 64, 64),
            WeigthsUtil.Slice(this.Rho, 64, 64),
            0.01f,
            $"{this.Name}-2"
        );

        return new ALIFLayer[] { p1, p2 };
    }

    public override string ToString()
    {
        return $"ALIF - \"{this.Name}\"";
    }
}