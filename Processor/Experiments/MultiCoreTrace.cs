using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public class MultiCoreTrace
{
    // Reporting
    private MemReporter mem;
    private FileReporter spikeDelays;
    private FileReporter computeDelays;
    private FileReporter coreStats;
    private FileReporter transfers;
    private readonly string outputPath;

    private readonly MultiCore exp;
    private readonly SNN splittedSNN;
    private readonly int correct;
    private int nrSpikes;

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
        double coreEnergies = exp.Cores.Sum(c => c.Energy(1));
        Console.WriteLine("Energy: " + coreEnergies);
    }

    private void SetupReporters(MultiCore multi, string resultsFolder)
    {
        Directory.CreateDirectory(resultsFolder);

        mem = new MemReporter($"{resultsFolder}");
        mem.RegisterSNN(splittedSNN);

        coreStats = new FileReporter($"{resultsFolder}/core-stats.csv");
        coreStats.ReportLine("name,ts,util,spikes_prod,spikes_cons,spikes_received,sops,core_spikes_dropped,input_spikes_dropped,early_spikes,late_spikes,receiver_busy,ALU_busy,sender_busy");

        spikeDelays = new FileReporter($"{resultsFolder}/spike-delays.csv");
        spikeDelays.ReportLine($"layer,delay,nr_hops");

        computeDelays = new FileReporter($"{resultsFolder}/compute-delays.csv");
        computeDelays.ReportLine($"core,delay");

        int myTS = 0;


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
                
                // TODO: Fix
                coreStats.ReportLine($"{c.Name()},{myTS},{util},{core.nrSpikesProduced},{core.nrSpikesConsumed},{core.nrSpikesReceived},{core.nrSOPs},{core.nrEarlySpikes},{core.nrLateSpikes},{core.receiverBusy},{core.ALUBusy},{core.senderBusy}");
            }

            // Acounting to go to the next TS
            myTS++;
        };

        foreach (var c in multi.Cores)
        {
            if (c is not CoreV1) continue;
            var core = c as CoreV1;

            core.OnSyncEnded += (_, ts, layer) =>
            {
                mem.AdvanceLayer(layer, ts, layer.Readout());
            };
            core.OnSpikeReceived += (time, layer, neuron, feedback, spike, nrHops) =>
            {
                spikeDelays.ReportLine($"{layer.Name},{time - spike.CreatedAt},{nrHops}");
            };
            core.OnSpikeSent += (time, fromLayer, neuron) =>
            {
                nrSpikes++;
            };
            core.OnSpikeComputed += (time, spike) =>
            {
                computeDelays.ReportLine($"{core.Name},{time - spike.ReceivedAt}");
            };
        }

        transfers = new FileReporter($"{resultsFolder}/transfers.csv");
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
        mem?.Finish();
        spikeDelays?.Finish();
        computeDelays?.Finish();
        transfers?.Finish();
        coreStats?.Finish();
        Console.WriteLine($"Nr spikes: {nrSpikes:n}");
    }
}