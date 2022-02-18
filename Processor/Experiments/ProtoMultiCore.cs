using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class ProtoMultiCore : Experiment
{
    private SRNN srnn;

    public MeshRouter[,] routers;
    public ProtoController controller;
    public List<Core> cores = new();

    private int bufferSize;
    private long interval;

    private TraceReporter trace;
    private TensorReporter tensor;
    private MemReporter mem;

    public int prediction = -1;
    public int correct = -1;

    public ProtoMultiCore(Simulator simulator, bool debug, int correct, SRNN srnn, long interval, int bufferSize) : base(simulator)
    {
        this.srnn = srnn;
        this.Debug = debug;
        this.correct = correct;
        this.bufferSize = bufferSize;
        this.interval = interval;
    }

    private void CreateRouters(int width, int height, MeshUtils.ConstructRouter createRouters)
    {
        routers = MeshUtils.CreateMesh(sim, width, height, createRouters);
    }

    private void AddController(SNN snn, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ProtoController(controllerCoord, 100, snn, 0, interval, name: "controller"));
        sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
        this.controller = controller;
    }

    private void AddCore(ProtoDelayModel delayModel, int size, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new ProtoCore(coreCoord, size, delayModel, name: name, feedbackBufferSize: bufferSize));
        sim.AddChannel(core.output, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, core.input);
        this.cores.Add(core);
    }

    private List<Core> GetPEs()
    {
        var newCores = new List<Core>(cores);
        newCores.Add(controller);
        return newCores;
    }

    private void AddReporters()
    {
        if (Debug)
        {
            trace = new TraceReporter("res/multi-core/proto/result.trace");

            mem = new MemReporter(srnn, "res/multi-core/proto");
            mem.RegisterSNN(srnn);

            tensor = new TensorReporter(srnn, "res/multi-core/proto");
            tensor.RegisterSNN(srnn);

            controller.TimeAdvanced += (_, ts) => trace.AdvanceTimestep(ts);
            controller.TimeAdvanced += (_, ts) =>
            {
                tensor.AdvanceTimestep(ts);
            };
        }

        foreach (var core in cores)
        {
            var protoCore = core as ProtoCore;

            protoCore.OnSyncEnded += (_, _, ts, layer) =>
            {
                float[] pots = (layer as ALIFLayer)?.Readout ?? (layer as OutputLayer)?.Readout;
                mem.AdvanceLayer(layer, ts, pots);
            };
            protoCore.OnSpikeReceived += (_, time, layer, neuron, feedback) => trace.InputSpike(neuron, time);
            protoCore.OnSpikeSent += (_, time, ev) =>
            {
                trace.OutputSpike(ev.Neuron, time);
                tensor.InformSpike(ev.Layer, ev.Neuron);
            };
            protoCore.OnSyncStarted += (_, time, _, _) => trace.TimeRef(time);
        }
    }

    public override void Setup()
    {
        // Hardware
        var delayModel = new ProtoDelayModel
        {
            InputTime = 7,
            ComputeTime = 2,
            OutputTime = 8,
            TimeRefTime = 2
        };
        CreateRouters(2, 2, (x, y) => new ProtoXYRouter(x, y, name: $"router({x},{y})"));
        AddController(srnn, 0, 0);
        AddCore(delayModel, 1024, 0, 1, "core1");
        AddCore(delayModel, 1024, 1, 1, "core2");
        AddCore(delayModel, 1024, 1, 0, "core3");

        // Reporters
        if (Debug)
        {
            AddReporters();
        }

        // Mapping
        var mapper = new ProtoMapper(srnn, GetPEs());
        var mapping = new MappingTable(srnn);
        mapper.OnMappingFound += mapping.Map;
        mapper.Run();

        foreach (var (layer, core) in mapping.Pairs)
        {
            if (core is not ProtoCore) continue;
            controller.LayerToCoord(layer, (MeshCoord)core.GetLocation());
        }

        foreach (var core in mapping.Cores)
        {
            if (core is not ProtoCore) continue;

            var destLayer = srnn.GetDestLayer(mapping[core].First());
            MeshCoord dest;
            if (destLayer == null)
                dest = (MeshCoord)controller.GetLocation();
            else
                dest = (MeshCoord)mapping[destLayer].GetLocation();

            ((ProtoCore)core).setDestination(dest);
        }

        simStop.StopEvents = 10_000_000;
    }

    public override void Cleanup()
    {
        this.prediction = srnn.Prediction();
        trace?.Finish();
        tensor?.Finish();
        mem?.Finish();
        if (Debug)
        {
            Console.WriteLine($"Nr spikes: {tensor.NrSpikes:n}");
            Console.WriteLine($"Predicted: {this.prediction}, Truth: {this.correct}");
        }
    }
}