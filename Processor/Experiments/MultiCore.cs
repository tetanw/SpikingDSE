using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE;

public class MultiCoreMapping
{
    public static Mapping CreateMapping(Mapper mapper, HWSpec spec, SNN snn)
    {
        foreach (var coreSpec in spec.Cores)
        {
            if (coreSpec is not CoreV1Spec) continue;

            mapper.AddCore(new MapCore
            {
                Name = coreSpec.Name,
                AcceptedTypes = new() { typeof(ALIFLayer), typeof(OutputLayer) },
                MaxNrNeurons = coreSpec.MaxNeurons
            });
        }
        var controllerSpec = spec.FindByType<ControllerV1Spec>();
        mapper.AddCore(new MapCore
        {
            Name = controllerSpec.Name,
            AcceptedTypes = new() { typeof(InputLayer) },
            MaxNrNeurons = controllerSpec.MaxNeurons
        });

        foreach (var layer in snn.GetAllLayers())
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

public class MultiCore : Experiment
{
    public Action SetupDone;
    public SNN snn;

    public MeshRouter[,] Routers;
    public ControllerV1 Controller;
    public List<Core> Cores = new();

    private Mapping mapping;
    private ISpikeSource source;
    private HWSpec spec;

    public MultiCore(ISpikeSource source, SNN snn, Mapping mapping, HWSpec spec)
    {
        this.snn = snn;
        this.source = source;
        this.mapping = mapping;
        this.spec = spec;
    }

    private void CreateRouters(int width, int height, MeshUtils.ConstructRouter createRouters)
    {
        Routers = MeshUtils.CreateMesh(sim, width, height, createRouters);
    }

    private void AddController(int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var input = snn.GetInputLayer();
        var controller = sim.AddActor(new ControllerV1(input, source, controllerCoord, spec.FindByType<ControllerV1Spec>()));
        this.Controller = controller;
        var mergeSplit = MeshUtils.ConnectMergeSplit(sim, Routers);
        sim.AddChannel(mergeSplit.ToController, controller.Input);
        sim.AddChannel(controller.Output, mergeSplit.FromController);
        sim.AddChannel(mergeSplit.ToMesh, Routers[0, 0].inWest);
    }

    private void AddCore(CoreV1Spec coreSpec)
    {
        var parts = coreSpec.ConnectsTo.Split(",");
        var x = int.Parse(parts[0]);
        var y = int.Parse(parts[1]);

        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new CoreV1(coreCoord, coreSpec));
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
        var mappingTable = new MappingTable(snn);
        foreach (var entry in mapping.Mapped)
        {
            var core = FindCore(entry.Core);
            var layer = snn.FindLayer(entry.Layer);
            if (layer == null)
            {
                string name = $"{entry.Layer}-{entry.Index}";
                layer = snn.FindLayer(name);
            }
            if (layer == null)
            {
                string name = $"{entry.Layer}-1";
                layer = snn.FindLayer(name);
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

    private static MeshRouter CreateRouter(int x, int y, MeshSpec mesh)
    {
        return new XYRouter(x, y, mesh);
    }

    public override void Setup()
    {
        // Hardware
        var mesh = spec.NoC as MeshSpec;
        CreateRouters(mesh.Width, mesh.Height, (x, y) => CreateRouter(x, y, mesh));
        AddController(-1, 0);
        foreach (var coreSpec in spec.Cores)
        {
            if (coreSpec is not CoreV1Spec) continue;

            AddCore(coreSpec as CoreV1Spec);
        }

        // Mapping
        ApplyMapping();

        SetupDone?.Invoke();
    }

    public override void Cleanup() { }

    public int Predict() => snn.GetOutputLayer().Prediction();
}