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
    private readonly int offset;
    public int TS { get; private set; }

    public ALIFLayer(float[,] inWeights, float[,] recWeights, float[] bias, float[] alpha, float[] rho, float VTh, string name, int offset = 0)
    {
        InputSize = inWeights.GetLength(0);
        Size = inWeights.GetLength(1);
        Pots = new float[Size];
        Readout = new float[Size];
        Spiked = new bool[Size];
        InWeights = inWeights;
        RecWeights = recWeights;
        Name = name;
        AdaptThr = new float[Size];
        Array.Fill(AdaptThr, VTh);
        Bias = bias;
        Alpha = alpha;
        Rho = rho;
        this.VTh = VTh;
        Beta = 1.8f;
        this.offset = offset;
    }

    public override void Forward(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += InWeights[neuron, dst];
        }
    }

    public override bool Sync(int dst)
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
        }
        else
        {
            Spiked[dst] = false;
        }

        // Leakage for next ts
        pot *= Alpha[dst];

        // Writeback
        Pots[dst] = pot;

        return Spiked[dst];
    }

    public override void FinishSync()
    {
        TS++;
    }

    public override string ToString()
    {
        return $"ALIF - \"{this.Name}\"";
    }

    public override Layer Copy()
    {
        return new ALIFLayer(this.InWeights, this.RecWeights, this.Bias, this.Alpha, this.Rho, this.VTh, this.Name, offset: this.offset);
    }

    public override Layer Slice(int start, int end, int partNr)
    {
        var sliceSize = end - start;
        var slice = new ALIFLayer(
            WeigthsUtil.Slice(this.InWeights, start, 0, sliceSize, this.InputSize),
            WeigthsUtil.Slice(this.RecWeights, start, 0, sliceSize, Size),
            WeigthsUtil.Slice(this.Bias, start, sliceSize),
            WeigthsUtil.Slice(this.Alpha, start, sliceSize),
            WeigthsUtil.Slice(this.Rho, start, sliceSize),
            0.01f,
            $"{this.Name}-{partNr}",
            offset: start
        );

        return slice;
    }

    public override bool IsRecurrent() => true;

    public override int Offset() => this.offset;

    public override void Feedback(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += RecWeights[neuron, dst];
        }
    }
}