using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreTrace
{
    // Reporting
    private MemReporter mem;
    private readonly string outputPath;

    private readonly MultiCore exp;
    private readonly SNN splittedSNN;
    private readonly int correct;

    public MultiCoreTrace(string snnPath, string hwPath, string mappingPath, string datasetPath, string traceName, string outputPath)
    {
        var snn = SNN.Load(snnPath);
        var hw = HWSpec.Load(hwPath);
        var mapping = Mapping.Load(mappingPath);
        mapping.PrintReport();

        splittedSNN = SNN.SplitSNN(snn, mapping);
        var shd = new ZipDataset(datasetPath);
        var inputFile = shd.ReadEntry(traceName);
        shd.Dispose();
        correct = inputFile.Correct;
        this.outputPath = outputPath;
        exp = new MultiCore(inputFile, splittedSNN, mapping, hw);
    }

    public void Run()
    {
        exp.SetupDone += () => SetupReporters(exp, outputPath);
        exp.Run();
        Console.WriteLine($"Predicted: {exp.Predict()}, Truth: {correct}");
        CleanupReporters();
    }

    private void SetupReporters(MultiCore multi, string resultsFolder)
    {
        Directory.CreateDirectory(resultsFolder);

        mem = new MemReporter($"{resultsFolder}");
        mem.RegisterSNN(splittedSNN);

        foreach (var hidden in splittedSNN.GetAllLayers().Where(c => c is HiddenLayer).Cast<HiddenLayer>())
        {
            hidden.SyncFinished += () => mem.AdvanceLayer(hidden);
        }
    }


    private void CleanupReporters()
    {
        mem?.Finish();
    }
}