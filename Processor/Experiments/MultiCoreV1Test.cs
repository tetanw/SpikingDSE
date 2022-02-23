using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public class MultiCoreV1Test
{
    // Reporting
    private TraceReporter trace;
    private TensorReporter spikes;
    private MemReporter mem;
    private TimeDelayReporter spikeDelays;
    private TimeDelayReporter computeDelays;
    private FileReporter coreStats;
    private FileReporter transfers;

    private MultiCoreV1 exp;
    private int correct;

    public MultiCoreV1Test()
    {
        var srnn = SRNN.Load("res/snn/best", 700, 2);
        var mapping = MultiCoreV1Mapping.CreateMapping(new FirstFitMapper(), srnn);
        mapping.PrintReport();

        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700, 100);
        this.correct = inputFile.Correct;
        var splittedSRNN = SplittedSRNN.SplitSRNN(srnn, mapping);
        this.exp = new MultiCoreV1(inputFile, splittedSRNN, mapping, 100_000, 512);
    }

    public void Run()
    {
        exp.SetupDone += () => SetupReporters(exp, "res/multi-core/v1");
        exp.Run();
        Console.WriteLine($"Predicted: {exp.Predict()}, Truth: {correct}");
        CleanupReporters();
    }

    private void SetupReporters(MultiCoreV1 multi, string resultsFolder)
    {
        Directory.CreateDirectory(resultsFolder);

        transfers = new FileReporter($"{resultsFolder}/transfers.csv");
        transfers.ReportLine($"hw-time,snn-time,router-x,router-y,from,to");
        coreStats = new FileReporter($"{resultsFolder}/core-stats.csv");
        coreStats.ReportLine("core_x,core_y,ts,util,spikes_prod,spikes_cons,sops");

        trace = new TraceReporter($"{resultsFolder}/result.trace");

        mem = new MemReporter(multi.srnn, $"{resultsFolder}");
        mem.RegisterSNN(multi.srnn);

        spikes = new TensorReporter(multi.srnn, $"{resultsFolder}");
        spikes.RegisterSNN(multi.srnn);

        spikeDelays = new TimeDelayReporter($"{resultsFolder}/spike-delays.csv");
        computeDelays = new TimeDelayReporter($"{resultsFolder}/compute-delays.csv");

        int myTS = 0;

        multi.Controller.TimeAdvanced += (_, _, ts) => trace.AdvanceTimestep(ts);
        multi.Controller.TimeAdvanced += (_, time, ts) =>
        {
            foreach (var c in multi.Cores)
            {
                var core = c as CoreV1;

                long timeBusy;
                if (core.lastSpike < time - multi.Interval)
                {
                    timeBusy = 0;
                }
                else
                {
                    timeBusy = core.lastSpike - (time - multi.Interval);
                }
                double util = (double)timeBusy / multi.Interval;
                var coord = (MeshCoord)c.GetLocation();
                coreStats.ReportLine($"{coord.x},{coord.y},{myTS},{util},{core.nrSpikesProduced},{core.nrSpikesConsumed},{core.nrSOPs}");
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