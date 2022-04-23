using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class ALIFQLayer : HiddenLayer
{
    public long[,] InWeights;
    public long[,] RecWeights;
    public long[] Pots;
    public bool[] Spiked;
    public long VTh;
    public float[] ReadoutArr;
    public long Beta;
    public long[] Bias;
    public long[] AdaptThr;
    public long[] Alpha;
    public long[] Rho;
    private readonly int offset;
    public int TS { get; private set; }
    public long Scale { get; set; }

    public ALIFQLayer(long scale, long[,] inWeights, long[,] recWeights, long[] bias, long[] alpha, long[] rho, long VTh, long Beta, string name, int offset = 0)
    {
        Scale = scale;
        InputSize = inWeights.GetLength(0);
        Size = inWeights.GetLength(1);
        Pots = new long[Size];
        ReadoutArr = new float[Size];
        Spiked = new bool[Size];
        InWeights = inWeights;
        RecWeights = recWeights;
        Name = name;
        AdaptThr = new long[Size];
        Array.Fill(AdaptThr, VTh);
        Bias = bias;
        Alpha = alpha;
        Rho = rho;
        this.VTh = VTh;
        this.Beta = Beta;
        this.offset = offset;
    }

    public override void Forward(int neuron)
    {
        if (neuron > InputSize)
            throw new Exception($"Neuron id {neuron} bigger than input size {InputSize}");

        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += InWeights[neuron, dst];
        }
    }

    public override bool Sync(int dst)
    {
        long pot = Pots[dst];

        // Adapt
        AdaptThr[dst] = AdaptThr[dst] * Rho[dst];
        if (Spiked[dst])
        {
            AdaptThr[dst] += 1 - Rho[dst];
        }
        AdaptThr[dst] /= Scale;

        // Reset potential
        long resetPot = (Beta * AdaptThr[dst] + VTh) / Scale;

        // Reset
        if (Spiked[dst])
            pot -= resetPot;

        // Readout
        ReadoutArr[dst] = pot;

        // Threshold
        long thrPot = resetPot - Bias[dst];
        if (pot >= thrPot)
        {
            Spiked[dst] = true;
        }
        else
        {
            Spiked[dst] = false;
        }

        // Leakage for next ts
        pot = pot * Alpha[dst] / Scale;

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
        return $"ALIF - \"{Name}\"";
    }

    public override Layer Copy()
    {
        return new ALIFQLayer(Scale, InWeights, RecWeights, Bias, Alpha, Rho, VTh, Beta, Name, offset: offset);
    }

    public override Layer Slice(int start, int end, int partNr)
    {
        var sliceSize = end - start;
        var slice = new ALIFQLayer(
            Scale,
            WeigthsUtil.Slice(InWeights, start, 0, sliceSize, this.InputSize),
            WeigthsUtil.Slice(RecWeights, start, 0, sliceSize, Size),
            WeigthsUtil.Slice(Bias, start, sliceSize),
            WeigthsUtil.Slice(Alpha, start, sliceSize),
            WeigthsUtil.Slice(Rho, start, sliceSize),
            VTh,
            Beta,
            $"{Name}-{partNr}",
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

    public override float[] Readout() => ReadoutArr;
}