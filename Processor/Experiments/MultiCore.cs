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
    private MergeSplit mergeSplit;
    public Bus Bus;
    public ControllerV1 Controller;
    public List<Core> Cores = new();

    private readonly Mapping mapping;
    private readonly ISpikeSource source;
    private readonly HWSpec spec;

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

    private static object ToCoord(string connectsTo)
    {
        var parts = connectsTo.Split(",");
        var type = parts[0];

        if (type == "mesh")
        {
            var x = int.Parse(parts[1]);
            var y = int.Parse(parts[2]);
            return new MeshCoord(x, y);
        }
        else if (type == "bus")
        {
            return int.Parse(parts[1]);
        }
        else
        {
            throw new Exception($"Unknown type: {type}");
        }
    }

    private void AddController(ControllerV1Spec spec)
    {
        var loc = ToCoord(spec.ConnectsTo);
        var input = snn.GetInputLayer();
        Controller = sim.AddActor(new ControllerV1(input, source, loc, spec));
        Cores.Add(Controller);
    }

    private void AddCore(CoreV1Spec coreSpec)
    {
        var loc = ToCoord(coreSpec.ConnectsTo);
        var core = sim.AddActor(new CoreV1(loc, coreSpec));
        Cores.Add(core);
    }

    private Core FindCore(string name)
    {
        var core = Cores.Find(c => c.Name() == name);
        return core;
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

    private void BuildNoc()
    {
        if (spec.NoC is MeshSpec)
        {
            var mesh = spec.NoC as MeshSpec;
            CreateRouters(mesh.Width, mesh.Height, (x, y) => new XYRouter(x, y, mesh));
        }
        else if (spec.NoC is BusSpec)
        {
            var busSpec = spec.NoC as BusSpec;
            this.Bus = sim.AddActor(new Bus(busSpec));
        }
    }

    private void ConnectNoC()
    {
        if (Routers != null)
        {
            mergeSplit = MeshUtils.ConnectMergeSplit(sim, Routers);
            var meshSpec = spec.NoC as MeshSpec;
            foreach (var core in Cores)
            {
                var meshLoc = (MeshCoord)core.GetLocation();
                var (x, y) = meshLoc;

                int width = meshSpec.Width;
                int height = meshSpec.Height;
                if (MeshUtils.InMesh(width, height, meshLoc))
                {
                    sim.AddChannel(core.Output(), Routers[x, y].inLocal);
                    sim.AddChannel(Routers[x, y].outLocal, core.Input());
                }
                else
                {
                    sim.AddChannel(mergeSplit.ToController, core.Input());
                    sim.AddChannel(core.Output(), mergeSplit.FromController);
                    // TODO: Do not hardcode router that it is connected to
                    sim.AddChannel(mergeSplit.ToMesh, Routers[0, 0].inWest);
                }
            }
        }
        else if (Bus != null)
        {
            foreach (var core in Cores)
            {
                var busLoc = (int)core.GetLocation();
                sim.AddChannel(core.Output(), Bus.Inputs[busLoc]);
                sim.AddChannel(Bus.Outputs[busLoc], core.Input());
            }
        }
    }

    public override void Setup()
    {
        // Build NoC
        BuildNoc();

        // Build all cores
        foreach (var coreSpec in spec.Cores)
        {
            if (coreSpec is CoreV1Spec)
                AddCore(coreSpec as CoreV1Spec);
            else if (coreSpec is ControllerV1Spec)
                AddController(coreSpec as ControllerV1Spec);
        }

        // Connect cores to NoC
        ConnectNoC();

        // Mapping
        ApplyMapping();

        SetupDone?.Invoke();
    }

    public override void Cleanup() { }

    public int Predict() => snn.GetOutputLayer().Prediction();
}