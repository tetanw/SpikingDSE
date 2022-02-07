using System;
using System.Collections.Generic;

namespace SpikingDSE;

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

    public void AddController(SNN snn, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ControllerV1(controllerCoord, 100, 0, interval, name: "controller"));
        sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
        this.controller = controller;
    }

    public void AddCore(V1DelayModel delayModel, int size, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new CoreV1(coreCoord, size, delayModel, name: name, feedbackBufferSize: bufferSize));
        sim.AddChannel(core.output, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, core.input);
        this.cores.Add(core);
    }

    public List<Core> GetPEs()
    {
        var newCores = new List<Core>(cores);
        newCores.Add(controller);
        return newCores;
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

    public int prediction = -1;
    public int correct = -1;

    public MultiCoreV1(Simulator simulator, bool debug, int correct, SplittedSRNN srnn, long interval, int bufferSize) : base(simulator)
    {
        this.srnn = srnn;
        this.Debug = debug;
        this.correct = correct;
        this.bufferSize = bufferSize;
        this.interval = interval;
    }

    private void AddReporters()
    {
        if (Debug)
        {
            trace = new TraceReporter("res/multi-core/v1/result.trace");

            mem = new MemReporter(srnn, "res/multi-core/v1");
            mem.RegisterSNN(srnn);

            spikes = new TensorReporter(srnn, "res/multi-core/v1");
            spikes.RegisterSNN(srnn);

            hw.controller.TimeAdvanced += (_, ts) => trace.AdvanceTimestep(ts);
            hw.controller.TimeAdvanced += (_, ts) =>
            {
                spikes.AdvanceTimestep(ts);
            };
        }

        foreach (var c in hw.cores)
        {
            var core = c as CoreV1;

            core.OnSyncEnded += (_, _, ts, layer) =>
            {
                float[] pots = (layer as ALIFLayer)?.Readout ?? (layer as OutputLayer)?.Readout;
                mem.AdvanceLayer(layer, ts, pots);
            };
            core.OnSpikeReceived += (_, time, layer, neuron, feedback) => trace.InputSpike(neuron, time);
            core.OnSpikeSent += (_, time, fromLayer, neuron) =>
            {
                trace.OutputSpike(neuron, time);
                spikes.InformSpike(fromLayer, neuron);
            };
            core.OnSyncStarted += (_, time, _, _) => trace.TimeRef(time);
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
        hw = new MulitCoreV1HW(sim, 3, 2, interval, bufferSize);
        hw.CreateRouters((x, y) => new ProtoXYRouter(x, y, name: $"router({x},{y})"));
        hw.AddController(srnn, 0, 0);
        hw.AddCore(delayModel, 64, 1, 0, "core1");
        hw.AddCore(delayModel, 64, 1, 1, "core2");
        hw.AddCore(delayModel, 64, 2, 0, "core3");
        hw.AddCore(delayModel, 64, 2, 1, "core4");
        hw.AddCore(delayModel, 20, 0, 1, "core0");

        // Reporters
        if (Debug)
        {
            AddReporters();
        }

        // Mapping
        var mapper = new FirstFitMapper(srnn, hw.GetPEs());
        var mapping = new Mapping(srnn);
        mapper.OnMappingFound += (core, layer) =>
        {
            if (Debug) Console.WriteLine($"  {layer} -> {core}");
            mapping.Map(core, layer);
        };
        if (Debug) Console.WriteLine("Mapping:");
        mapper.Run();

        foreach (var core in mapping.Cores)
        {
            switch (core)
            {
                case CoreV1 coreV1:
                    coreV1.LoadMapping(mapping);
                    break;
                case ControllerV1 contV1:
                    contV1.LoadMapping(mapping);
                    break;
                default:
                    throw new Exception("Unknown core type: " + core);
            }

        }

        simStop.StopEvents = 10_000_000;
    }

    public override void Cleanup()
    {
        this.prediction = srnn.Prediction();
        trace?.Finish();
        spikes?.Finish();
        mem?.Finish();
        if (Debug)
        {
            Console.WriteLine($"Nr spikes: {spikes.NrSpikes:n}");
            Console.WriteLine($"Predicted: {this.prediction}, Truth: {this.correct}");
        }
    }
}