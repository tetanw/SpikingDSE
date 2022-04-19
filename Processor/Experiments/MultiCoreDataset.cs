using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreDataset : DSEExperiment<MultiCore>, IDisposable
{
    private readonly Mapping mapping;
    private readonly HWSpec hw;
    private readonly SNN snn;
    private readonly ZipDataset dataset;
    private readonly string outputDir;

    private int nrCorrect = 0;
    private int nrDone = 0;
    private readonly int maxSamples = 0;
    private int sampleCounter = 0;
    private readonly Stopwatch sampleCounterSw;
    private readonly Stopwatch lastProgress;

    private readonly List<long> latencies = new();
    private readonly List<(int, int)> predictions = new();

    public MultiCoreDataset(string snnPath, string hwPath, string mappingPath, string datasetPath, string outputDir, int maxSamples)
    {
        mapping = Mapping.Load(mappingPath);
        snn = SNN.SplitSNN(SNN.Load(snnPath), mapping);
        hw = HWSpec.Load(hwPath);
        dataset = new ZipDataset(datasetPath);
        this.maxSamples = maxSamples == -1 ? dataset.NrSamples : maxSamples;
        sampleCounterSw = new Stopwatch();
        sampleCounterSw.Start();
        lastProgress = new Stopwatch();
        lastProgress.Start();
        UpdateProgressBar(first: true);
        this.outputDir = outputDir;
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
        // Done with progressbar
        ClearCurrentConsoleLine();

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);


        var acc = (float)nrCorrect / maxSamples * 100;
        Console.WriteLine($"Samples: {maxSamples}");
        Console.WriteLine($"Accuracy: {acc}");
        Console.WriteLine($"Running time: {(int)runningTime.TotalMilliseconds:n}ms");
        double avgLat = latencies.Sum() / maxSamples;
        double maxLat = latencies.Max();
        double minLat = latencies.Min();
        Console.WriteLine($"Latency:");
        Console.WriteLine($"  Avg: {avgLat:n} cycles");
        Console.WriteLine($"  Min: {minLat:n} cycles");
        Console.WriteLine($"  Max: {maxLat:n} cycles");

        var expRep = new FileReporter($"{outputDir}/experiments.csv");
        expRep.ReportLine("exp,latency,correct,predicted");
        for (int i = 0; i < maxSamples; i++)
        {
            expRep.ReportLine($"{i},{latencies[i]},{predictions[i].Item1},{predictions[i].Item2}");
        }
        expRep.Finish();
    }

    public override void WhenSampleDone(MultiCore exp)
    {
        int correct = (int)exp.Context;
        if (exp.Predict() == correct)
        {
            nrCorrect++;
        }

        latencies.Add(exp.Latency);
        predictions.Add((correct, exp.Predict()));

        nrDone++;


        UpdateProgressBar();
    }

    private void UpdateProgressBar(bool first = false)
    {
        if (first)
        {
            Console.Write($"Progress: {nrDone} / {maxSamples}");
            return;
        }

        sampleCounter++;
        if (lastProgress.ElapsedMilliseconds > 3000)
        {

            ClearCurrentConsoleLine();
            double sampleRate = sampleCounter / sampleCounterSw.Elapsed.TotalSeconds;
            int samplesLeft = maxSamples - nrDone;
            double timeLeft = samplesLeft / sampleRate;
            Console.Write($"Progress: {nrDone} / {maxSamples}, Sample rate: {(int)sampleRate} samples/s, Expected time left: {(int)timeLeft}s");
            lastProgress.Restart();

            if (sampleCounter >= 100)
            {
                sampleCounter = 0;
                sampleCounterSw.Restart();
            }
        }

    }

    private static void ClearCurrentConsoleLine()
    {
        int currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, currentLineCursor);
    }

    public void Dispose()
    {
        dataset.Dispose();
        GC.SuppressFinalize(this);
    }
}