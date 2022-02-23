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
    public Action SetupDone;
    public SplittedSRNN srnn;

    public MeshRouter[,] Routers;
    public ControllerV1 Controller;
    public List<Core> Cores = new();

    public int BufferSize { get; set; }
    public long Interval { get; set; }


    private Mapping mapping;
    private ISpikeSource source;

    public MultiCoreV1(ISpikeSource source, SplittedSRNN srnn, Mapping mapping, long interval, int bufferSize)
    {
        this.srnn = srnn;
        this.source = source;
        this.BufferSize = bufferSize;
        this.Interval = interval;
        this.mapping = mapping;
    }

    private void CreateRouters(int width, int height, MeshUtils.ConstructRouter createRouters)
    {
        Routers = MeshUtils.CreateMesh(sim, width, height, createRouters);
    }

    private void AddController(InputLayer input, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ControllerV1(input, source, controllerCoord, 100, 0, Interval, name: "controller"));
        this.Controller = controller;
        var mergeSplit = MeshUtils.ConnectMergeSplit(sim, Routers);
        sim.AddChannel(mergeSplit.ToController, controller.Input);
        sim.AddChannel(controller.Output, mergeSplit.FromController);
        sim.AddChannel(mergeSplit.ToMesh, Routers[0, 0].inWest);
    }

    private void AddCore(V1DelayModel delayModel, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new CoreV1(coreCoord, delayModel, name: name, feedbackBufferSize: BufferSize));
        sim.AddChannel(core.output, Routers[x, y].inLocal);
        sim.AddChannel(Routers[x, y].outLocal, core.input);
        this.Cores.Add(core);
    }

    private Core FindCore(string name)
    {
        var core = Cores.Find(c => c.Name() == name);
        if (core != null)
            return core;

        if (name == Controller.Name)
            return Controller;

        return null;
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

        // Mapping
        ApplyMapping();

        SetupDone?.Invoke();
    }

    public override void Cleanup() { }

    public int Predict() => srnn.Prediction();
}