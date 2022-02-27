using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public class MultiCoreTest
{
    // Reporting
    private TraceReporter trace;
    private TensorReporter spikes;
    private MemReporter mem;
    private TimeDelayReporter spikeDelays;
    private TimeDelayReporter computeDelays;
    private FileReporter coreStats;
    private FileReporter transfers;

    private MultiCore exp;
    private SNN splittedSNN;
    private int correct;

    public MultiCoreTest()
    {
        var snn = SNN.Load("data/best-snn.json");
        var hw =  HWSpec.Load("./data/mesh-hw.json");
        var mapping = Mapping.Load("./data/mapping.json");
        mapping.PrintReport();

        splittedSNN = SNN.SplitSNN(snn, mapping);
        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700, 100);
        this.correct = inputFile.Correct;
        this.exp = new MultiCore(inputFile, splittedSNN, mapping, hw);
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
        transfers.ReportLine($"hw-time,snn-time,router-x,router-y,from,to");
        coreStats = new FileReporter($"{resultsFolder}/core-stats.csv");
        coreStats.ReportLine("core_x,core_y,ts,util,spikes_prod,spikes_cons,sops,core_spikes_dropped,input_spikes_dropped,late_spikes");

        trace = new TraceReporter($"{resultsFolder}/result.trace");

        mem = new MemReporter(splittedSNN, $"{resultsFolder}");
        mem.RegisterSNN(splittedSNN);

        spikes = new TensorReporter(splittedSNN, $"{resultsFolder}");
        spikes.RegisterSNN(splittedSNN);

        spikeDelays = new TimeDelayReporter($"{resultsFolder}/spike-delays.csv");
        computeDelays = new TimeDelayReporter($"{resultsFolder}/compute-delays.csv");

        int myTS = 0;

        multi.Controller.TimeAdvanced += (_, _, ts) => trace.AdvanceTimestep(ts);
        multi.Controller.TimeAdvanced += (_, time, ts) =>
        {
            long interval = multi.Controller.spec.Interval;
            foreach (var c in multi.Cores)
            {
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
                var coord = (MeshCoord)c.GetLocation();
                coreStats.ReportLine($"{coord.x},{coord.y},{myTS},{util},{core.nrSpikesProduced},{core.nrSpikesConsumed},{core.nrSOPs},{core.nrSpikesDroppedCore},{core.nrSpikesDroppedInput},{core.nrLateSpikes}");
            }

            // Acounting to go to the next TS
            spikes.AdvanceTimestep(ts);
            myTS++;
        };

        foreach (var c in multi.Cores)
        {
            var core = c as CoreV1;

            core.OnSyncEnded += (_, ts, layer) =>
            {
                float[] pots = (layer as ALIFLayer)?.Readout ?? (layer as OutputLayer)?.Readout;
                mem.AdvanceLayer(layer, ts, pots);
            };
            core.OnSpikeReceived += (time, layer, neuron, feedback, spike, nrHops) =>
            {
                trace.InputSpike(neuron, time);
                spikeDelays.ReportDelay(spike.CreatedAt, time, layer.Name, nrHops.ToString());
            };
            core.OnSpikeSent += (time, fromLayer, neuron, _) =>
            {
                trace.OutputSpike(neuron, time);
                spikes.InformSpike(fromLayer, neuron);
            };
            core.OnSpikeComputed += (time, spike) =>
            {
                computeDelays.ReportDelay(spike.ReceivedAt, time, "");
            };
            core.OnSyncStarted += (time, _, _) => trace.TimeRef(time);
        }

        foreach (var r in multi.Routers)
        {
            var router = r as XYRouter;

            router.OnTransfer += (time, from, to) =>
            {
                transfers.ReportLine($"{time},{myTS},{router.x},{router.y},{from},{to}");
            };
        }
    }


    private void CleanupReporters()
    {
        trace?.Finish();
        spikes?.Finish();
        mem?.Finish();
        spikeDelays?.Finish();
        computeDelays?.Finish();
        transfers?.Finish();
        coreStats?.Finish();
        if (spikes != null) Console.WriteLine($"Nr spikes: {spikes.NrSpikes:n}");
    }
}