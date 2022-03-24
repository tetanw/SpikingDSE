using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MultiCoreDSE : DSEExperiment<MultiCore>, IDisposable
{
    private readonly Mapping mapping;
    private readonly HWSpec hw;
    private readonly SNN snn;
    private readonly SNN splittedSnn;
    private readonly ZipDataset dataset;
    private int nrCorrect = 0;
    private int curBufferSize = -1;

    public MultiCoreDSE(string snnPath, string hwPath, string mappingPath, string datasetPath)
    {
        snn = SNN.Load(snnPath);
        mapping = Mapping.Load(mappingPath);
        splittedSnn = SNN.SplitSNN(snn, mapping);
        hw = HWSpec.Load(hwPath);
        dataset = new ZipDataset(datasetPath);
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
        for (int i = 0; i < dataset.NrSamples; i++)
        {
            var inputFile = dataset.ReadEntry($"input_{i}.trace"); // TODO: Harcoded input dataset size
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
        var acc = (float)nrCorrect / dataset.NrSamples;
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