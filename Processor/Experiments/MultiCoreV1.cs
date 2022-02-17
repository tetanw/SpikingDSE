using System;
using System.Collections.Generic;

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
                MaxNrNeurons = 64
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

public class MulitCoreV1HW
{
    private Simulator sim;

    public MeshRouter[,] routers;
    public ControllerV1 controller;
    public List<Core> cores = new();

    private long interval;
    private int bufferSize;

    public int width, height;

    public MulitCoreV1HW(Simulator sim, int width, int height, long interval, int bufferSize)
    {
        this.sim = sim;
        this.width = width;
        this.height = height;
        this.interval = interval;
        this.bufferSize = bufferSize;
    }

    public void CreateRouters(MeshUtils.ConstructRouter createRouters)
    {
        routers = MeshUtils.CreateMesh(sim, width, height, createRouters);
    }

    public void AddController(InputLayer input, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ControllerV1(input, controllerCoord, 100, 0, interval, name: "controller"));
        this.controller = controller;
        var mergeSplit = MeshUtils.ConnectMergeSplit(sim, routers);
        sim.AddChannel(mergeSplit.ToController, controller.Input);
        sim.AddChannel(controller.Output, mergeSplit.FromController);
        sim.AddChannel(mergeSplit.ToMesh, routers[0, 0].inWest);
    }

    public void AddCore(V1DelayModel delayModel, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new CoreV1(coreCoord, delayModel, name: name, feedbackBufferSize: bufferSize));
        sim.AddChannel(core.output, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, core.input);
        this.cores.Add(core);
    }

    public Core FindCore(string name)
    {
        var core = cores.Find(c => c.Name() == name);
        if (core != null)
            return core;

        if (name == controller.Name)
            return controller;

        return null;
    }
}

public class MultiCoreV1 : Experiment
{
    private SplittedSRNN srnn;
    private int bufferSize;
    private long interval;

    private MulitCoreV1HW hw;
    private TraceReporter trace;
    private TensorReporter spikes;
    private MemReporter mem;
    private TimeDelayReporter spikeDelays;
    private TimeDelayReporter computeDelays;
    private FileReporter coreUtils;
    private FileReporter transfers;
    private Mapping mapping;

    public int Prediction = -1;
    public int Correct = -1;

    public MultiCoreV1(Simulator simulator, bool debug, int correct, SplittedSRNN srnn, Mapping mapping, long interval, int bufferSize) : base(simulator)
    {
        this.srnn = srnn;
        this.Debug = debug;
        this.Correct = correct;
        this.bufferSize = bufferSize;
        this.interval = interval;
        this.mapping = mapping;
    }

    private void AddReporters()
    {
        if (!Debug)
            return;

        transfers = new FileReporter("res/multi-core/v1/transfers.csv");
        coreUtils = new FileReporter("res/multi-core/v1/util-core.csv");
        coreUtils.ReportLine("core_x,core_y,util");

        trace = new TraceReporter("res/multi-core/v1/result.trace");

        mem = new MemReporter(srnn, "res/multi-core/v1");
        mem.RegisterSNN(srnn);

        spikes = new TensorReporter(srnn, "res/multi-core/v1");
        spikes.RegisterSNN(srnn);

        spikeDelays = new TimeDelayReporter("res/multi-core/v1/spike-delays.csv");
        computeDelays = new TimeDelayReporter("res/multi-core/v1/compute-delays.csv");

        int myTS = 0;

        hw.controller.TimeAdvanced += (_, _, ts) => trace.AdvanceTimestep(ts);
        hw.controller.TimeAdvanced += (_, time, ts) =>
        {
            myTS++;
            spikes.AdvanceTimestep(ts);
            foreach (var c in hw.cores)
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
                coreUtils.ReportLine($"{coord.x},{coord.y},{util}");
            }
        };

        foreach (var c in hw.cores)
        {
            var core = c as CoreV1;

            core.OnSyncEnded += (_, ts, layer) =>
            {
                float[] pots = (layer as ALIFLayer)?.Readout ?? (layer as OutputLayer)?.Readout;
                mem.AdvanceLayer(layer, ts, pots);
            };
            core.OnSpikeReceived += (time, layer, neuron, feedback, spike) =>
            {
                trace.InputSpike(neuron, time);
                spikeDelays.ReportDelay(spike.CreatedAt, time, layer.Name);
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

        transfers.ReportLine($"hw-time,router-x,router-y,from,to,snn-time");
        foreach (var r in hw.routers)
        {
            var router = r as XYRouter;

            router.OnTransfer += (time, from, to) =>
            {
                transfers.ReportLine($"{time},{router.x},{router.y},{from},{to},{myTS}");
            };
        }
    }

    private void ApplyMapping()
    {
        var mappingTable = new MappingTable(srnn);
        foreach (var entry in mapping.Mapped)
        {
            var core = hw.FindCore(entry.Core.Name);
            string name = entry.Partial ? $"{entry.Layer.Name}-{entry.Index}" : entry.Layer.Name;
            var layer = srnn.FindLayer(name);
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
            OutputTime = 8,
            TimeRefTime = 2
        };
        hw = new MulitCoreV1HW(sim, 5, 2, interval, bufferSize);
        hw.CreateRouters((x, y) => new XYRouter(x, y, 3, 5, 10, name: $"router({x},{y})"));
        hw.AddController(srnn.Input, -1, 0);
        hw.AddCore(delayModel, 0, 0, "core1");
        hw.AddCore(delayModel, 0, 1, "core2");
        hw.AddCore(delayModel, 1, 0, "core3");
        hw.AddCore(delayModel, 1, 1, "core4");
        hw.AddCore(delayModel, 2, 0, "core5");
        hw.AddCore(delayModel, 2, 1, "core6");
        hw.AddCore(delayModel, 3, 0, "core7");
        hw.AddCore(delayModel, 3, 1, "core8");
        hw.AddCore(delayModel, 4, 0, "core9");
        hw.AddCore(delayModel, 4, 1, "core10");

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
        coreUtils?.Finish();
        if (spikes != null) PrintLn($"Nr spikes: {spikes.NrSpikes:n}");
        PrintLn($"Predicted: {this.Prediction}, Truth: {this.Correct}");
    }
}