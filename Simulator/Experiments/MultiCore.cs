using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE;

public class MultiCore : Experiment
{
    public Action SetupDone;
    public SNN snn;

    public List<Core> Cores = new();
    public Comm Comm;

    private readonly Mapping mapping;
    private readonly ISpikeSource source;
    private readonly HWSpec spec;

    public long Latency { get; set; }

    public MultiCore(ISpikeSource source, SNN snn, Mapping mapping, HWSpec spec)
    {
        this.snn = snn;
        this.source = source;
        this.mapping = mapping;
        this.spec = spec;
    }

    private Core FindCore(string name)
    {
        var core = Cores.Find(c => c.Name == name);
        return core;
    }

    private void ApplyMapping()
    {
        var mappingMan = new MappingManager(snn);
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
            mappingMan.Map(core, layer);
        }
        mappingMan.ControllerCoord = Cores.Find(c => c is Controller).Location;
        mappingMan.Cores = Cores;

        // Load stuff
        foreach (var core in mappingMan.Cores)
            core.Mapping = mappingMan;
    }

    public override void Setup()
    {
        // Build all cores
        foreach (var coreSpec in spec.Cores)
        {
            var location = spec.NoC.ToCoord(coreSpec.ConnectsTo);
            var core = coreSpec.Build();
            core.Location = location;
            core.Input = new();
            core.Output = new();
            core.Name = coreSpec.Name;
            if (core is Controller cont)
                cont.AddSource(source);
            sim.AddActor(core);
            Cores.Add(core);
        }

        // Build NoC
        Comm = spec.NoC.Build(sim, Cores);

        // Mapping
        ApplyMapping();

        SetupDone?.Invoke();
    }

    public override void Cleanup()
    {
        Latency = sim.Now;
    }

    public int Predict() => snn.GetOutputLayer().Prediction();
}