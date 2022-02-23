using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public class MultiCoreV1Mapping
{
    public static Mapping CreateMapping(Mapper mapper, SRNN srnn)
    {
        for (int i = 1; i <= 10; i++)
        {
            mapper.AddCore(new MapCore
            {
                Name = $"core{i}",
                AcceptedTypes = new() { typeof(ALIFLayer), typeof(OutputLayer) },
                MaxNrNeurons = 128
            });
        }
        mapper.AddCore(new MapCore
        {
            Name = "controller",
            AcceptedTypes = new() { typeof(InputLayer) },
            MaxNrNeurons = int.MaxValue
        });

        foreach (var layer in srnn.GetAllLayers())
        {
            mapper.AddLayer(new MapLayer
            {
                Name = layer.Name,
                NrNeurons = layer.Size,
                Splittable = layer is ALIFLayer,
                Type = layer.GetType()
            });
        }

        return mapper.Run();
    }
}

public class MultiCoreV1 : Experiment
{
    private SplittedSRNN srnn;

    public MeshRouter[,] routers;
    public ControllerV1 controller;
    public List<Core> cores = new();

    private int bufferSize;
    private long interval;
    private string resultsFolder;

    private TraceReporter trace;
    private TensorReporter spikes;
    private MemReporter mem;
    private TimeDelayReporter spikeDelays;
    private TimeDelayReporter computeDelays;
    private FileReporter coreStats;
    private FileReporter transfers;
    private Mapping mapping;

    public int Prediction = -1;
    public int Correct = -1;

    public MultiCoreV1(bool debug, string resultsFolder, int correct, SplittedSRNN srnn, Mapping mapping, long interval, int bufferSize)
    {
        this.srnn = srnn;
        this.Debug = debug;
        this.resultsFolder = resultsFolder;
        this.Correct = correct;
        this.bufferSize = bufferSize;
        this.interval = interval;
        this.mapping = mapping;
    }

    private void CreateRouters(int width, int height, MeshUtils.ConstructRouter createRouters)
    {
        routers = MeshUtils.CreateMesh(sim, width, height, createRouters);
    }

    private void AddController(InputLayer input, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ControllerV1(input, controllerCoord, 100, 0, interval, name: "controller"));
        this.controller = controller;
        var mergeSplit = MeshUtils.ConnectMergeSplit(sim, routers);
        sim.AddChannel(mergeSplit.ToController, controller.Input);
        sim.AddChannel(controller.Output, mergeSplit.FromController);
        sim.AddChannel(mergeSplit.ToMesh, routers[0, 0].inWest);
    }

    private void AddCore(V1DelayModel delayModel, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new CoreV1(coreCoord, delayModel, name: name, feedbackBufferSize: bufferSize));
        sim.AddChannel(core.output, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, core.input);
        this.cores.Add(core);
    }

    private Core FindCore(string name)
    {
        var core = cores.Find(c => c.Name() == name);
        if (core != null)
            return core;

        if (name == controller.Name)
            return controller;

        return null;
    }

    private void AddReporters()
    {
        if (!Debug)
            return;

        Directory.CreateDirectory(resultsFolder);

        transfers = new FileReporter($"{resultsFolder}/transfers.csv");
        transfers.ReportLine($"hw-time,snn-time,router-x,router-y,from,to");
        coreStats = new FileReporter($"{resultsFolder}/core-stats.csv");
        coreStats.ReportLine("core_x,core_y,ts,util,spikes_prod,spikes_cons,sops");

        trace = new TraceReporter($"{resultsFolder}/result.trace");

        mem = new MemReporter(srnn, $"{resultsFolder}");
        mem.RegisterSNN(srnn);

        spikes = new TensorReporter(srnn, $"{resultsFolder}");
        spikes.RegisterSNN(srnn);

        spikeDelays = new TimeDelayReporter($"{resultsFolder}/spike-delays.csv");
        computeDelays = new TimeDelayReporter($"{resultsFolder}/compute-delays.csv");

        int myTS = 0;

        controller.TimeAdvanced += (_, _, ts) => trace.AdvanceTimestep(ts);
        controller.TimeAdvanced += (_, time, ts) =>
        {
            foreach (var c in cores)
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
                coreStats.ReportLine($"{coord.x},{coord.y},{myTS},{util},{core.nrSpikesProduced},{core.nrSpikesConsumed},{core.nrSOPs}");
            }

            // Acounting to go to the next TS
            spikes.AdvanceTimestep(ts);
            myTS++;
        };

        foreach (var c in cores)
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

        foreach (var r in routers)
        {
            var router = r as XYRouter;

            router.OnTransfer += (time, from, to) =>
            {
                transfers.ReportLine($"{time},{myTS},{router.x},{router.y},{from},{to}");
            };
        }
    }

    private void ApplyMapping()
    {
        var mappingTable = new MappingTable(srnn);
        foreach (var entry in mapping.Mapped)
        {
            var core = FindCore(entry.Core.Name);
            var layer = srnn.FindLayer(entry.Layer.Name);
            if (layer == null)
            {
                string name = $"{entry.Layer.Name}-{entry.Index}";
                layer = srnn.FindLayer(name);
            }
            if (layer == null)
            {
                string name = $"{entry.Layer.Name}-1";
                layer = srnn.FindLayer(name);
            }
            mappingTable.Map(core, layer);
        }

        // Load stuff
        foreach (var core in mappingTable.Cores)
        {
            switch (core)
            {
                case CoreV1 coreV1:
                    coreV1.LoadMapping(mappingTable);
                    break;
                case ControllerV1 contV1:
                    contV1.LoadMapping(mappingTable);
                    break;
                default:
                    throw new Exception("Unknown core type: " + core);
            }

        }
    }

    public override void Setup()
    {
        // Hardware
        var delayModel = new V1DelayModel
        {
            InputTime = 7,
            ComputeTime = 2,
            OutputTime = 8
        };
        CreateRouters(5, 2, (x, y) => new XYRouter(x, y, 3, 5, 16, name: $"router({x},{y})"));
        AddController(srnn.Input, -1, 0);
        AddCore(delayModel, 0, 0, "core1");
        AddCore(delayModel, 0, 1, "core2");
        AddCore(delayModel, 1, 0, "core3");
        AddCore(delayModel, 1, 1, "core4");
        AddCore(delayModel, 2, 0, "core5");
        AddCore(delayModel, 2, 1, "core6");
        AddCore(delayModel, 3, 0, "core7");
        AddCore(delayModel, 3, 1, "core8");
        AddCore(delayModel, 4, 0, "core9");
        AddCore(delayModel, 4, 1, "core10");

        // Reporters
        AddReporters();

        // Mapping
        ApplyMapping();

        simStop.StopEvents = 10_000_000;
    }

    public override void Cleanup()
    {
        this.Prediction = srnn.Prediction();
        trace?.Finish();
        spikes?.Finish();
        mem?.Finish();
        spikeDelays?.Finish();
        computeDelays?.Finish();
        transfers?.Finish();
        coreStats?.Finish();
        if (spikes != null) PrintLn($"Nr spikes: {spikes.NrSpikes:n}");
        PrintLn($"Predicted: {this.Prediction}, Truth: {this.Correct}");
    }
}