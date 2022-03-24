using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public class MultiCoreTest
{
    // Reporting
    private TensorReporter spikes;
    private MemReporter mem;
    private TimeDelayReporter spikeDelays;
    private TimeDelayReporter computeDelays;
    private FileReporter coreStats;
    private FileReporter transfers;

    private readonly MultiCore exp;
    private readonly SNN splittedSNN;
    private readonly int correct;

    public MultiCoreTest(string snnPath, string hwPath, string mappingPath, string datasetPath, string traceName, string outputPath)
    {
        var snn = SNN.Load(snnPath);
        var hw = HWSpec.Load(hwPath);
        var mapping = Mapping.Load(mappingPath);
        mapping.PrintReport();

        splittedSNN = SNN.SplitSNN(snn, mapping);
        var shd = new ZipDataset(datasetPath);
        var inputFile = shd.ReadEntry(traceName, 700); // TODO: Harcoded input size
        shd.Dispose();
        correct = inputFile.Correct;
        exp = new MultiCore(inputFile, splittedSNN, mapping, hw);
    }

    public void Run()
    {
        exp.SetupDone += () => SetupReporters(exp, "res/results/v1");
        exp.Run();
        Console.WriteLine($"Predicted: {exp.Predict()}, Truth: {correct}");
        CleanupReporters();
    }

    private void SetupReporters(MultiCore multi, string resultsFolder)
    {
        Directory.CreateDirectory(resultsFolder);

        transfers = new FileReporter($"{resultsFolder}/transfers.csv");
        coreStats = new FileReporter($"{resultsFolder}/core-stats.csv");

        mem = new MemReporter($"{resultsFolder}");
        mem.RegisterSNN(splittedSNN);

        spikes = new TensorReporter(splittedSNN, $"{resultsFolder}");
        spikes.RegisterSNN(splittedSNN);

        spikeDelays = new TimeDelayReporter($"{resultsFolder}/spike-delays.csv");
        computeDelays = new TimeDelayReporter($"{resultsFolder}/compute-delays.csv");

        int myTS = 0;


        coreStats.ReportLine("name,ts,util,spikes_prod,spikes_cons,spikes_received,sops,core_spikes_dropped,input_spikes_dropped,late_spikes,core_energy_spent");
        multi.Controller.TimeAdvanced += (_, time, ts) =>
        {
            long interval = multi.Controller.spec.Interval;
            foreach (var c in multi.Cores)
            {
                if (c is not CoreV1) continue;
                var core = c as CoreV1;

                long timeBusy;
                if (core.lastSpike < time - interval)
                {
                    timeBusy = 0;
                }
                else
                {
                    timeBusy = core.lastSpike - (time - interval);
                }
                double util = (double)timeBusy / interval;
                coreStats.ReportLine($"{c.Name()},{myTS},{util},{core.nrSpikesProduced},{core.nrSpikesConsumed},{core.nrSpikesReceived},{core.nrSOPs},{core.nrSpikesDroppedCore},{core.nrSpikesDroppedInput},{core.nrLateSpikes},{core.energySpent}");
            }

            // Acounting to go to the next TS
            spikes.AdvanceTimestep(ts);
            myTS++;
        };

        foreach (var c in multi.Cores)
        {
            if (c is not CoreV1) continue;
            var core = c as CoreV1;

            core.OnSyncEnded += (_, ts, layer) =>
            {
                float[] pots = (layer as ALIFLayer)?.Readout ?? (layer as OutputLayer)?.Readout;
                mem.AdvanceLayer(layer, ts, pots);
            };
            core.OnSpikeReceived += (time, layer, neuron, feedback, spike, nrHops) =>
            {
                spikeDelays.ReportDelay(spike.CreatedAt, time, layer.Name, nrHops.ToString());
            };
            core.OnSpikeSent += (time, fromLayer, neuron, _) =>
            {
                spikes.InformSpike(fromLayer, neuron);
            };
            core.OnSpikeComputed += (time, spike) =>
            {
                computeDelays.ReportDelay(spike.ReceivedAt, time, "");
            };
        }

        if (multi.Routers != null)
        {
            transfers.ReportLine($"hw-time,snn-time,router-x,router-y,from,to");
            foreach (var r in multi.Routers)
            {
                var router = r as XYRouter;

                router.OnTransfer += (time, from, to) =>
                {
                    transfers.ReportLine($"{time},{myTS},{router.x},{router.y},{from},{to}");
                };
            }
        }
        else if (multi.Bus != null)
        {
            transfers.ReportLine($"hw-time,snn-time,from,to");
            multi.Bus.OnTransfer += (time, from, to) =>
            {
                transfers.ReportLine($"{time},{myTS},{from},{to}");
            };
        }
    }


    private void CleanupReporters()
    {
        spikes?.Finish();
        mem?.Finish();
        spikeDelays?.Finish();
        computeDelays?.Finish();
        transfers?.Finish();
        coreStats?.Finish();
        if (spikes != null) Console.WriteLine($"Nr spikes: {spikes.NrSpikes:n}");
    }
}