using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class ProtoMultiCore : Experiment
{
    private SRNN srnn;
    private int bufferSize;
    private long interval;

    private MulitCoreHW hw;
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

    private void AddReporters()
    {
        if (Debug)
        {
            trace = new TraceReporter("res/multi-core/result.trace");

            mem = new MemReporter(srnn.snn, "res/multi-core");
            mem.RegisterSNN(srnn.snn);

            tensor = new TensorReporter(srnn.snn, "res/multi-core");
            tensor.RegisterSNN(srnn.snn);

            hw.controller.TimeAdvanced += (_, ts) => trace.AdvanceTimestep(ts);
            hw.controller.TimeAdvanced += (_, ts) =>
            {
                tensor.AdvanceTimestep(ts);
            };
        }

        foreach (var core in hw.cores)
        {
            var protoCore = core as ProtoCore;

            protoCore.OnSyncEnded += (_, _, ts, layer) =>
            {
                float[] pots = (layer as ALIFLayer)?.Readout ?? (layer as OutputIFLayer)?.Readout;
                mem.AdvanceLayer(layer, ts, pots);
            };
            protoCore.OnSpikeReceived += (_, time, layer, neuron, feedback) => trace.InputSpike(neuron, time);
            protoCore.OnSpikeSent += (_, time, ev) =>
            {
                trace.OutputSpike(ev.neuron, time);
                tensor.InformSpike(ev.layer, ev.neuron);
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
        hw = new MulitCoreHW(sim, 2, 2, interval, bufferSize);
        hw.CreateRouters((x, y) => new ProtoXYRouter(x, y, name: $"router({x},{y})"));
        hw.AddController(srnn.snn, 0, 0);
        hw.AddCore(delayModel, 1024, 0, 1, "core1");
        hw.AddCore(delayModel, 1024, 1, 1, "core2");
        hw.AddCore(delayModel, 1024, 1, 0, "core3");

        // Reporters
        if (Debug)
        {
            AddReporters();
        }

        // Mapping
        var mapper = new FirstFitMapper(srnn.snn, hw.GetPEs());
        var mapping = new Mapping();
        mapper.OnMappingFound += mapping.Map;
        mapper.Run();

        foreach (var (layer, core) in mapping._forward)
        {
            if (core is not ProtoCore) continue;
            hw.controller.LayerToCoord(layer, (MeshCoord)core.GetLocation());
        }

        foreach (var core in mapping.Cores)
        {
            if (core is not ProtoCore) continue;

            var destLayer = srnn.snn.GetDestLayer(mapping.Reverse[core]);
            MeshCoord dest;
            if (destLayer == null)
                dest = (MeshCoord)hw.controller.GetLocation();
            else
                dest = (MeshCoord)mapping.Forward[destLayer].GetLocation();

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

public class ProtoMultiCoreTest : Experiment
{
    private ProtoMultiCore exp;

    public ProtoMultiCoreTest()
    {
        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700);
        var srnn = new SRNN("res/snn/best", inputFile);
        this.exp = new ProtoMultiCore(sim, true, inputFile.Correct, srnn, 100_000_000, int.MaxValue);
    }

    public override void Setup()
    {
        exp.Setup();
    }

    public override void Cleanup()
    {
        exp.Cleanup();
    }
}