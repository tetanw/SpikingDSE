using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreDataset : BatchExperiment<MultiCore>
{
    struct ExpRes
    {
        public long Latency;
    }

    record struct MemoryEntry(string Name, double NrBits);

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
    private bool first = true;

    private readonly ExpRes[] expResList;
    private readonly StreamWriter logFile;
    private readonly FileReporter expRep;

    public MultiCoreDataset(string snnPath, string hwPath, string mappingPath, string datasetPath, string outputDir, int maxSamples)
    {
        mapping = Mapping.Load(mappingPath);
        snn = SNN.SplitSNN(SNN.Load(snnPath), mapping);
        hw = HWSpec.Load(hwPath);
        dataset = new ZipDataset(datasetPath);
        this.maxSamples = Math.Min(maxSamples, dataset.NrSamples);
        sampleCounterSw = new Stopwatch();
        sampleCounterSw.Start();
        expResList = new ExpRes[this.maxSamples];

        this.outputDir = outputDir;
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        logFile = new StreamWriter(File.Create($"{outputDir}/summary.log"));

        expRep = new FileReporter($"{outputDir}/experiments.csv");

        Report($"Input files:");
        Report($"  SNN: {snnPath}");
        Report($"  HW: {hwPath}");
        Report($"  Mapping: {mappingPath}");
        Report($"  Dataset: {datasetPath}");
        Report($"  Output: {outputDir}");

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
        Report($"Samples: {maxSamples}");
        Report($"Accuracy: {acc}");
        Report($"Running time: {(int)runningTime.TotalMilliseconds:n}ms");
        List<long> latencies = expResList.Select(res => res.Latency).ToList();
        double avgLat = latencies.Sum() / maxSamples;
        double maxLat = latencies.Max();
        double minLat = latencies.Min();
        Report($"Latency:");
        Report($"  Avg: {avgLat:n}");
        Report($"  Min: {minLat:n}");
        Report($"  Max: {maxLat:n}");

        logFile.Dispose();
        dataset.Dispose();
        expRep.Finish();
    }

    private void Report(string line)
    {
        Console.WriteLine(line);
        logFile.WriteLine(line);
    }

    private static IEnumerable<T> Flatten<T>(T[,] map)
    {
        for (int row = 0; row < map.GetLength(0); row++)
        {
            for (int col = 0; col < map.GetLength(1); col++)
            {
                yield return map[row, col];
            }
        }
    }

    private string ConstructHeader(MultiCore exp)
    {
        var parts = new List<string>();
        parts.Add($"expNr,runningTime,latency,correct,predicted");
        parts.AddRange(exp.Cores.SelectMany(c => c.Report(exp.Latency, first)));
        parts.AddRange(exp.Comm.Report(first));
        return string.Join(',', parts);
    }

    public override void WhenSampleDone(MultiCore exp, long expNr, TimeSpan sampleTime)
    {
        int correct = (int)exp.Context;
        if (exp.Predict() == correct)
        {
            nrCorrect++;
        }
        nrDone++;
        var runningTime = sampleTime.TotalMilliseconds;

        if (first)
        {
            string header = ConstructHeader(exp);
            expRep.ReportLine(header);
            first = false;
        }

        var parts = new List<string>();
        parts.Add($"{expNr},{runningTime},{exp.Latency},{correct},{exp.Predict()}");
        parts.AddRange(exp.Cores.SelectMany(c => c.Report(exp.Latency, first)));
        parts.AddRange(exp.Comm.Report(first));
        expRep.ReportLine(string.Join(',', parts));

        expResList[expNr] = new ExpRes
        {
            Latency = exp.Latency,
        };

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
        if (lastProgress.ElapsedMilliseconds > 1000)
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
}