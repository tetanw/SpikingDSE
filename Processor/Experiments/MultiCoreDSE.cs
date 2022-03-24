using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreDSE : DSEExperiment<MultiCore>, IDisposable
{
    private readonly Mapping mapping;
    private readonly List<HWSpec> hws;
    private readonly SNN snn;
    private readonly ZipDataset dataset;
    private int nrCorrect = 0;

    public MultiCoreDSE(string snnPath, List<string> hwPaths, string mappingPath, string datasetPath)
    {
        mapping = Mapping.Load(mappingPath);
        snn = SNN.SplitSNN(SNN.Load(snnPath), mapping);
        hws = hwPaths.Select(p => HWSpec.Load(p)).ToList();
        dataset = new ZipDataset(datasetPath);
    }

    public override IEnumerable<IEnumerable<MultiCore>> Configs()
    {
        foreach (var hw in hws)
        {
            yield return With(hw);
        }
    }

    public IEnumerable<MultiCore> With(HWSpec hw)
    {
        for (int i = 0; i < dataset.NrSamples; i++)
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

    public override void OnConfigCompleted(TimeSpan runningTime)
    {
        var acc = (float)nrCorrect / dataset.NrSamples;
        Console.WriteLine($"{acc};{(int)runningTime.TotalMilliseconds}ms");
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