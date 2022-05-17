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
    public float[] ReadoutArr;
    public float Beta;
    public float[] Bias;
    public float[] AdaptThr;
    public float[] Alpha;
    public float[] Rho;
    private readonly int offset;
    public int TS { get; private set; }

    public ALIFLayer(float[,] inWeights, float[,] recWeights, float[] bias, float[] alpha, float[] rho, float beta, float VTh, string name, int offset = 0)
    {
        InputSize = inWeights.GetLength(0);
        Size = inWeights.GetLength(1);
        Pots = new float[Size];
        ReadoutArr = new float[Size];
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
        Beta = beta;
        this.offset = offset;
        Splittable = true;
        TypeName = "ALIF";
        Recurrent = true;
        NrSynapses = Size * InputSize + Size * Size;
    }

    public override void Forward(int neuron)
    {
        if (neuron > InputSize)
            throw new Exception($"Neuron id {neuron} bigger than input size {InputSize}");

        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += InWeights[neuron, dst]; // +
        }

        Ops.AddCount("Addf32", Size);
    }

    public override void Feedback(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += RecWeights[neuron, dst]; // +
        }

        Ops.AddCount("Addf32", Size);
    }

    public override bool Sync(int dst)
    {
        float pot = Pots[dst];

        // Adapt
        AdaptThr[dst] = AdaptThr[dst] * Rho[dst]; // *
        if (Spiked[dst])
        {
            AdaptThr[dst] += 1 - Rho[dst]; // +, -
        }

        // Reset potential
        float resetPot = Beta * AdaptThr[dst] + VTh; // *, +

        // Reset
        if (Spiked[dst])
            pot -= resetPot; // -

        // Readout
        ReadoutArr[dst] = pot;

        // Threshold
        float thrPot = resetPot - Bias[dst]; // -
        if (pot >= thrPot) // Sub
        {
            Spiked[dst] = true;
        }
        else
        {
            Spiked[dst] = false;
        }

        // Leakage for next ts
        pot *= Alpha[dst]; // *

        // Writeback
        Pots[dst] = pot;

        return Spiked[dst];
    }

    public override void FinishSync()
    {
        TS++;

        // Ops.AddCount("Addf32", Size);
        // Ops.AddCount("Addf32", Size);
        // Ops.AddCount("Addf32", Size);
        // Ops.AddCount("Addf32", Size);
        // Ops.AddCount("Addf32", Size);
        // Ops.AddCount("Addf32", Size);
    }

    public override string ToString()
    {
        return $"ALIF - \"{Name}\"";
    }

    public override Layer Copy()
    {
        return new ALIFLayer(InWeights, RecWeights, Bias, Alpha, Rho, Beta, VTh, Name, offset: offset);
    }

    public override Layer Slice(int start, int end, int partNr)
    {
        var sliceSize = end - start;
        var slice = new ALIFLayer(
            WeigthsUtil.Slice(InWeights, start, 0, sliceSize, InputSize),
            WeigthsUtil.Slice(RecWeights, start, 0, sliceSize, Size),
            WeigthsUtil.Slice(Bias, start, sliceSize),
            WeigthsUtil.Slice(Alpha, start, sliceSize),
            WeigthsUtil.Slice(Rho, start, sliceSize),
            Beta,
            VTh,
            $"{Name}-{partNr}",
            offset: start
        );

        return slice;
    }

    public override int Offset() => this.offset;

    public override float[] Readout() => ReadoutArr;
}