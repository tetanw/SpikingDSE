using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreDataset : DSEExperiment<MultiCore>, IDisposable
{
    private readonly Mapping mapping;
    private readonly HWSpec hw;
    private readonly SNN snn;
    private readonly ZipDataset dataset;
    private int nrCorrect = 0;
    private readonly int maxSamples = 0;

    private readonly List<long> latencies = new();

    public MultiCoreDataset(string snnPath, string hwPath, string mappingPath, string datasetPath, int maxSamples)
    {
        mapping = Mapping.Load(mappingPath);
        snn = SNN.SplitSNN(SNN.Load(snnPath), mapping);
        hw = HWSpec.Load(hwPath);
        dataset = new ZipDataset(datasetPath);
        this.maxSamples = maxSamples == -1 ? dataset.NrSamples : maxSamples;
    }

    public override IEnumerable<MultiCore> Exp()
    {
        for (int i = 0; i < maxSamples; i++)
        {
            var inputFile = dataset.ReadEntry($"input_{i}.trace");
            var copy = snn.Copy();
            var exp = new MultiCore(inputFile, copy, mapping, hw)
            {
                Debug = false,
                Context = inputFile.Correct
            };
            yield return exp;
        }
    }

    public override void WhenCompleted(TimeSpan runningTime)
    {
        var acc = (float)nrCorrect / maxSamples;
        Console.WriteLine($"Samples: {maxSamples}");
        Console.WriteLine($"Accuracy: {acc}");
        Console.WriteLine($"Running time: {(int)runningTime.TotalMilliseconds:n}ms");
        double avgLat = latencies.Sum() / maxSamples;
        double stdDevLat = Math.Sqrt(latencies.Sum((s) => Math.Pow(s - avgLat, 2)) / (maxSamples - 1));
        Console.WriteLine($"Latency:");
        Console.WriteLine($"  Avg: {avgLat:n} cycles");
        Console.WriteLine($"  Std dev: {stdDevLat:n} cycles");
        Console.WriteLine($"Compute requirements: -");
        Console.WriteLine($"Memory requirements: -");

        nrCorrect = 0;
    }

    public override void WhenSampleDone(MultiCore exp)
    {
        int correct = (int)exp.Context;
        if (exp.Predict() == correct)
        {
            nrCorrect++;
        }

        latencies.Add(exp.Latency);
    }

    public void Dispose()
    {
        dataset.Dispose();
        GC.SuppressFinalize(this);
    }
}