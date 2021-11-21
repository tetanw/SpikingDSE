using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MultiODIN2Test : Experiment
{
    private SNN snn;
    private MeshRouter[,] routers;
    private TraceReporter trace;
    private TensorReporter tensor;
    private MemReporter mem;

    private ODINController2 AddController(SNN snn, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ODINController2(controllerCoord, snn, 0, 1_000_000, name: "controller"));
        controller.TimeAdvanced += (_, ts) => trace.AdvanceTimestep(ts);
        controller.TimeAdvanced += (_, ts) =>
        {
            tensor.AdvanceTimestep(ts);
            Console.WriteLine($"Advanced to timestep: {ts}");
        };
        // controller.SpikeReceived += (_, _, spike) =>
        // {
        //     tensor.InformSpike(spike.layer, spike.neuron);
        // };
        sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
        return controller;
    }

    private ODINCore2 AddCore(ODINDelayModel delayModel, int size, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new ODINCore2(coreCoord, size, delayModel, name: name));
        core.OnTimeReceived += (_, _, ts, layer) =>
        {
            float[] pots = (layer as RLIFLayer)?.Pots ?? (layer as LIFLayer)?.Pots;
            mem.AdvanceLayer(layer, ts, pots);
        };
        core.OnSpikeReceived += (_, time, layer, neuron, feedback) => trace.InputSpike(neuron, time);
        core.OnSpikeSent += (_, time, ev) =>
        {
            trace.OutputSpike(ev.neuron, time);
            tensor.InformSpike(ev.layer, ev.neuron);
        };
        core.OnTimeReceived += (_, time, _, _) => trace.TimeRef(time);
        sim.AddChannel(core.output, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, core.input);
        return core;
    }

    public override void Setup()
    {
        // Extra assumptions to get ODIN working
        // 1. ODIN has a refractory mode
        // 2. Layer is also transmitted
        // 3. Both excitatory and inhibitory spikes can be used together
        // 4. Leakage is proportional to current voltage instead of a constant????

        string folderPath = "res/multi-odin/validation/small";
        trace = new TraceReporter("res/multi-odin/result.trace");

        // SNN
        float alpha = (float)Math.Exp(-1.0 * 1.0 / 10.0);
        float beta = 1 - alpha;
        snn = new SNN();
        var input = new InputLayer(new TensorFile($"res/multi-odin/validation/input_0.csv"), name: "input");
        snn.AddLayer(input);
        var hidden1 = new RLIFLayer(
            WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_i_2_h1_n.csv", headers: true), scale: beta),
            WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h1_2_h1_n.csv", headers: true), scale: beta),
            name: "hidden1"
        );
        hidden1.Leakage = alpha;
        hidden1.Thr = 0.01f;
        hidden1.ResetMode = ResetMode.Subtract;
        snn.AddLayer(hidden1);

        var hidden2 = new RLIFLayer(
            WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h1_2_h2_n.csv", headers: true), scale: beta),
            WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h2_2_h2_n.csv", headers: true), scale: beta),
            name: "hidden2"
        );
        hidden2.Leakage = alpha;
        hidden2.Thr = 0.01f;
        hidden2.ResetMode = ResetMode.Subtract;
        snn.AddLayer(hidden2);

        // alpha = (float)Math.Exp(-1.0 * 1.0 / 15.0);
        // var output = new LIFLayer(
        //     WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h2o_n.csv", headers: true),
        //     name: "output"
        // );
        // output.leakage = alpha;
        // output.Thr = 0.00f;
        // output.ResetMode = ResetMode.Subtract;
        // snn.AddLayer(output);

        tensor = new TensorReporter(snn, "res/multi-odin/tensor");
        tensor.RegisterLayer(hidden1);
        tensor.RegisterLayer(hidden2);
        // tensor.RegisterLayer(output);

        mem = new MemReporter(snn, "res/multi-odin/mem");
        mem.RegisterLayer(hidden1);
        mem.RegisterLayer(hidden2);
        // mem.RegisterLayer(output);

        // Hardware
        int width = 3;
        int height = 2;
        var delayModel = new ODINDelayModel
        {
            InputTime = 7,
            ComputeTime = 2,
            OutputTime = 8,
            TimeRefTime = 2
        };

        routers = MeshUtils.CreateMesh(sim, width, height, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));

        var controller = AddController(snn, 0, 0);
        var core1 = AddCore(delayModel, 1024, 0, 1, "core1");
        var core2 = AddCore(delayModel, 1024, 1, 1, "core2");
        var core3 = AddCore(delayModel, 1024, 2, 1, "core3");

        // Mapping
        var mapper = new FirstFitMapper(snn, new Core[] { controller, core1, core2, core3 });
        var mapping = new Mapping();
        mapper.OnMappingFound += mapping.Map;
        mapper.Run();

        foreach (var (layer, core) in mapping._forward)
        {
            if (core is not ODINCore2) continue;
            controller.LayerToCoord(layer, (MeshCoord)core.GetLocation());
        }

        foreach (var core in mapping.Cores)
        {
            if (core is not ODINCore2) continue;

            var destLayer = snn.GetDestLayer(mapping.Reverse[core]);
            MeshCoord dest;
            if (destLayer == null)
                dest = (MeshCoord)controller.GetLocation();
            else
                dest = (MeshCoord)mapping.Forward[destLayer].GetLocation();

            ((ODINCore2)core).setDestination(dest);
        }

        simStop.StopEvents = 10_000_000;
    }

    public override void Cleanup()
    {
        // trace.Finish();
        tensor.Finish();
        mem.Finish();
        Console.WriteLine($"Nr spikes: {tensor.NrSpikes}");
    }
}