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
        Operations.AddCount("Add-f32", Size);
    }

    public override bool Sync(int dst)
    {
        float pot = Pots[dst];

        // Adapt
        AdaptThr[dst] = AdaptThr[dst] * Rho[dst]; // *
        if (Spiked[dst]) // cmp
        {
            AdaptThr[dst] += 1 - Rho[dst]; // +, -
        }

        // Reset potential
        float resetPot = Beta * AdaptThr[dst] + VTh; // *, +

        // Reset
        if (Spiked[dst]) // cmp
            pot -= resetPot; // -

        // Readout
        ReadoutArr[dst] = pot;

        // Threshold
        float thrPot = resetPot - Bias[dst]; // -
        if (pot >= thrPot) // cmp
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
        Operations.AddCount("Cmp-f32", Size * 3);
        Operations.AddCount("Add-f32", Size * 2);
        Operations.AddCount("Sub-f32", Size * 3);
        Operations.AddCount("Mul-f32", Size * 3);
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

    public override void Feedback(int neuron)
    {
        for (int dst = 0; dst < Size; dst++)
        {
            Pots[dst] += RecWeights[neuron, dst];
        }
    }

    public override float[] Readout() => ReadoutArr;
}