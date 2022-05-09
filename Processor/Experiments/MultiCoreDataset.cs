using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreDataset : BatchExperiment<MultiCore>, IDisposable
{
    struct ExpRes
    {
        public long Latency;
        public double Energy;
        public List<MemoryEntry> Memories;
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
        this.maxSamples = maxSamples == -1 ? dataset.NrSamples : maxSamples;
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
        Report($"Memory:");
        double totalBits = 0;
        foreach (var (Name, Bits) in expResList[0].Memories)
        {
            Report($"  {Name}: {Bits:n} bits");
            totalBits += Bits;
        }
        Console.WriteLine($"  Total: {totalBits:n} bits");
        List<long> latencies = expResList.Select(res => res.Latency).ToList();
        double avgLat = latencies.Sum() / maxSamples;
        double maxLat = latencies.Max();
        double minLat = latencies.Min();
        Report($"Latency:");
        Report($"  Avg: {avgLat:n} cycles");
        Report($"  Min: {minLat:n} cycles");
        Report($"  Max: {maxLat:n} cycles");
        List<double> energies = expResList.Select(res => res.Energy).ToList();
        double avgEnergy = energies.Sum() / maxSamples;
        double maxEnergy = energies.Max();
        double minEnergy = energies.Min();
        Report($"Energy:");
        Report($"  Avg: {Measurements.FormatSI(avgEnergy, "J")}");
        Report($"  Min: {Measurements.FormatSI(minEnergy, "J")}");
        Report($"  Max: {Measurements.FormatSI(maxEnergy, "J")}");

        logFile.Dispose();
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
            first = false;
            string reportString = $"expNr,runningTime,correct,predicted";
            foreach (var core in exp.Cores)
            {
                var coreStr = core.Report(true);
                if (coreStr != string.Empty)
                    reportString += $",{coreStr}";
            }
            foreach (var routers in Flatten(exp.Routers))
            {
                var routerStr = routers.Report(true);
                if (routerStr != string.Empty)
                    reportString += $",{routerStr}";
            }
            expRep.ReportLine(reportString);
        }
        else
        {
            string reportString = $"{expNr},{runningTime},{correct},{exp.Predict()}";
            foreach (var core in exp.Cores)
            {
                var coreStr = core.Report(false);
                if (coreStr != string.Empty)
                    reportString += $",{coreStr}";
            }
            foreach (var routers in Flatten(exp.Routers))
            {
                var routerStr = routers.Report(false);
                if (routerStr != string.Empty)
                    reportString += $",{routerStr}";
            }
            expRep.ReportLine(reportString);
        }

        expResList[expNr] = new ExpRes
        {
            Latency = exp.Latency,
            Energy = exp.Cores.Sum(c => c.Energy(exp.Latency))
        };
        if (expNr == 0)
        {
            var memories = exp.Cores.Select(c => new MemoryEntry(c.Name(), c.Memory())).ToList();
            memories.AddRange(Flatten(exp.Routers).Select(r => new MemoryEntry(r.Name, r.Memory())));
            expResList[expNr].Memories = memories;
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

    public void Dispose()
    {
        dataset.Dispose();
        expRep.Finish();
        GC.SuppressFinalize(this);
    }
}