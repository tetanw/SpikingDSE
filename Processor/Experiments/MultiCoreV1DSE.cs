using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SpikingDSE;

public class MultiCoreV1DSE : DSEExperiment<MultiCoreV1>
{
    private int size = 2264;
    private Mapping mapping;
    private SRNN srnn;
    private SplittedSRNN splittedSRNN = null;
    private int nrCorrect = 0;
    private int curBufferSize = -1;

    public MultiCoreV1DSE()
    {
        this.srnn = SRNN.Load("res/snn/best", 700, 2);
        this.mapping = MultiCoreV1Mapping.CreateMapping(new FirstFitMapper(), srnn);
        this.splittedSRNN = SplittedSRNN.SplitSRNN(srnn, this.mapping);
    }

    public override IEnumerable<IEnumerable<MultiCoreV1>> Configs()
    {
        // var sizes = Enumerable.Range(1, 64).Where(i => i % 2 == 1);
        // var sizes = new int[] { 1, 2, 4, 8, 16, 128, 256, 512, 2048 };
        var sizes = new int[] { 16384 };

        foreach (var size in sizes)
        {
            curBufferSize = size;
            yield return WithBufferSize(size);
        }
    }

    public IEnumerable<MultiCoreV1> WithBufferSize(int bufferSize)
    {
        for (int i = 0; i < size; i++)
        {
            var inputFile = new InputTraceFile($"res/shd/input_{i}.trace", 700, 100);
            var simulator = new Simulator();
            var copy = splittedSRNN.Copy();
            var exp = new MultiCoreV1(inputFile, copy, this.mapping, 100_000_000, bufferSize);
            exp.Debug = false;
            exp.Context = inputFile.Correct;
            yield return exp;
        }
    }

    public override void OnConfigCompleted(TimeSpan runningTime)
    {
        var acc = (float)nrCorrect / size;
        Console.WriteLine($"{curBufferSize};{acc};{(int)runningTime.TotalMilliseconds}ms");
        nrCorrect = 0;
    }

    public override void OnExpCompleted(MultiCoreV1 exp)
    {
        int correct = (int) exp.Context;
        if (exp.Predict() == correct)
        {
            nrCorrect++;
        }
    }
}