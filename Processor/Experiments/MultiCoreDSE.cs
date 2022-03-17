using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SpikingDSE;

public class MultiCoreDSE : DSEExperiment<MultiCore>, IDisposable
{
    private readonly int DATASET_SIZE = 2264;
    private readonly Mapping mapping;
    private readonly HWSpec hw;
    private readonly SNN snn;
    private readonly SNN splittedSnn;
    private readonly ZipDataset dataset;
    private int nrCorrect = 0;
    private int curBufferSize = -1;

    public MultiCoreDSE()
    {
        snn = SNN.Load("data/ssc-snn.json");
        mapping = Mapping.Load("data/mapping-ssc.json");
        splittedSnn = SNN.SplitSNN(snn, mapping);
        hw = HWSpec.Load("data/mesh-hw-big.json");
        dataset = new ZipDataset("res/ssc-4.zip");
    }

    public override IEnumerable<IEnumerable<MultiCore>> Configs()
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

    public IEnumerable<MultiCore> WithBufferSize(int bufferSize)
    {
        for (int i = 0; i < DATASET_SIZE; i++)
        {
            var inputFile = dataset.ReadEntry($"input_{i}.trace", 700);
            var copy = splittedSnn.Copy();
            var exp = new MultiCore(inputFile, copy, mapping, hw)
            {
                Debug = false,
                Context = inputFile.Correct
            };
            yield return exp;
        }
    }

    public override void OnConfigCompleted(TimeSpan runningTime)
    {
        var acc = (float)nrCorrect / DATASET_SIZE;
        Console.WriteLine($"{curBufferSize};{acc};{(int)runningTime.TotalMilliseconds}ms");
        nrCorrect = 0;
    }

    public override void OnExpCompleted(MultiCore exp)
    {
        int correct = (int)exp.Context;
        if (exp.Predict() == correct)
        {
            nrCorrect++;
        }
    }

    public void Dispose()
    {
        dataset.Dispose();
        GC.SuppressFinalize(this);
    }
}