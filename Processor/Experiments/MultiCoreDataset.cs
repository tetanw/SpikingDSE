using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreDataset : DSEExperiment<MultiCore>, IDisposable
{
    private readonly Mapping mapping;
    private readonly HWSpec hw;
    private readonly SNN snn;
    private readonly ZipDataset dataset;
    private int nrCorrect = 0;
    private int nrDone = 0;
    private readonly int maxSamples = 0;
    private int sampleCounter = 0;
    private readonly Stopwatch sampleCounterSw;
    private readonly Stopwatch lastProgress;

    private readonly List<long> latencies = new();
    private readonly UtilManager utilMan = new UtilManager();

    public MultiCoreDataset(string snnPath, string hwPath, string mappingPath, string datasetPath, int maxSamples)
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

        var expRep = new FileReporter("res/results/experiments.csv");
        expRep.ReportLine("exp,latency,energy");
        for (int i = 0; i < maxSamples; i++)
        {
            expRep.ReportLine($"{i},{latencies[i]},0");
        }
        expRep.Finish();

        var utilRep = new FileReporter("res/results/utilization.csv");
        utilRep.ReportLine("name,util");
        foreach (var (name, period) in utilMan.GetPeriods())
        {
            double util = (double)period.Busy / period.Period;
            utilRep.ReportLine($"{name},{util}");
        }
        utilRep.Finish();
    }

    public override void WhenSampleDone(MultiCore exp)
    {
        int correct = (int)exp.Context;
        if (exp.Predict() == correct)
        {
            nrCorrect++;
        }

        latencies.Add(exp.Latency);

        nrDone++;

        foreach (var c in exp.Cores)
        {
            if (c is not CoreV1 core)
                continue;

            utilMan.WriteBusyPeriod($"{core.Name}-receiver", core.receiverBusy, exp.Latency);
            utilMan.WriteBusyPeriod($"{core.Name}-sender", core.senderBusy, exp.Latency);
            utilMan.WriteBusyPeriod($"{core.Name}-alu", core.ALUBusy, exp.Latency);
        }

        foreach (var r in exp.Routers)
        {
            if (r is not XYRouter router)
                continue;

            for (int i = 0; i < 5; i++)
            {
                utilMan.WriteBusyPeriod($"{router.Name}-in{i}", router.inBusy[i], exp.Latency);
                utilMan.WriteBusyPeriod($"{router.Name}-out{i}", router.outBusy[i], exp.Latency);
            }
            utilMan.WriteBusyPeriod($"{router.Name}-switch", router.switchBusy, exp.Latency);
        }

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