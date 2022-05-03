using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

class HWFile
{
    public Dictionary<string, JsonElement> Global { get; set; }
    public Dictionary<string, JsonElement> NoC { get; set; }
    public Dictionary<string, JsonElement> CoreTemplates { get; set; }
    public List<Dictionary<string, JsonElement>> Cores { get; set; }
}

public class HWSpec
{
    public GlobalSpec Global { get; set; }
    public List<CoreSpec> Cores { get; set; }
    public NoCSpec NoC { get; set; }

    private static CoreSpec CreateCoreSpec(Dictionary<string, JsonElement> instance, Dictionary<string, JsonElement> templates, GlobalSpec global)
    {
        var templateName = instance.GetOptional("$Template")?.GetString();
        if (templateName != null)
        {
            var template = templates[templateName];
            foreach (var property in template.EnumerateObject())
            {
                instance[property.Name] = property.Value;
            }
        }

        string type = instance["Type"].GetString();
        CoreSpec core;
        if (type == "controller-v1")
        {
            core = new ControllerV1Spec
            {
                Interval = instance.GetOptional("Interval")?.GetInt64() ?? -1,
                GlobalSync = instance.GetOptional("GlobalSync")?.GetBoolean() ?? false,
                ConnectsTo = instance["ConnectsTo"].GetString(),
            };
        }
        else if (type == "core-v1")
        {
            core = new CoreV1Spec
            {
                IntegrateDelay = instance["IntegrateDelay"].GetInt32(),
                SyncDelay = instance["SyncDelay"].GetInt32(),
                ConnectsTo = instance["ConnectsTo"].GetString(),
                MaxNeurons = instance["MaxNeurons"].GetInt32(),
                IntegrateEnergy = instance["IntegrateEnergy"].GetDouble(),
                SyncEnergy = instance["SyncEnergy"].GetDouble(),
                StaticPower = instance["StaticPower"].GetDouble(),
                OutputBufferSize = instance["OutputBufferSize"].GetInt32(),
                NrParallel = instance["NrParallel"].GetInt32(),
                ReportSyncEnd = instance["ReportSyncEnd"].GetBoolean(),
            };
        }
        else
        {
            throw new Exception($"Invalid instance type: {type}");
        }

        core.Global = global;
        core.LayerCosts = new();
        if (instance.ContainsKey("LayerCosts"))
        {
            foreach (var pair in instance["LayerCosts"].EnumerateObject())
            {
                core.LayerCosts[pair.Name] = pair.Value.GetDouble();
            }
        }
        core.Name = instance["Name"].GetString();
        core.AcceptedTypes = instance.GetOptional("Accepts")?.GetStringArray() ?? Array.Empty<string>();
        core.Priority = instance.GetOptional("Priority")?.GetInt32() ?? int.MaxValue;
        core.MaxSynapses = instance.GetOptional("MaxSynapses")?.GetInt32() ?? int.MaxValue;
        core.MaxNeurons = instance.GetOptional("MaxNeurons")?.GetInt32() ?? int.MaxValue;
        core.MaxLayers = instance.GetOptional("MaxLayers")?.GetInt32() ?? int.MaxValue;
        core.MaxFanIn = instance.GetOptional("MaxFanIn")?.GetInt32() ?? int.MaxValue;
        core.MaxSplits = instance.GetOptional("MaxSplits")?.GetInt32() ?? int.MaxValue;

        return core;
    }

    private static NoCSpec CreateNoCSpec(Dictionary<string, JsonElement> instance, GlobalSpec global)
    {
        string type = "Mesh";
        NoCSpec noc;
        if (type == "Mesh")
        {
            noc = new MeshSpec
            {
                Width = instance["Width"].GetInt32(),
                Height = instance["Height"].GetInt32(),
                InputSize = instance["InputSize"].GetInt32(),
                OutputSize = instance["OutputSize"].GetInt32(),
                SwitchDelay = instance["SwitchDelay"].GetInt32(),
                TransferDelay = instance["TransferDelay"].GetInt32(),
                TransferEnergy = instance["TransferEnergy"].GetDouble(),
                StaticPower = instance["StaticPower"].GetDouble(),
                InputDelay = instance["InputDelay"].GetInt32(),
                OutputDelay = instance["OutputDelay"].GetInt32(),
            };
        }
        else if (type == "Bus")
        {
            noc = new BusSpec
            {
                Ports = instance["Ports"].GetInt32(),
                TransferDelay = instance["TransferDelay"].GetInt32(),
                TransferEnergy = instance["TransferEnergy"].GetDouble(),
                StaticPower = instance["StaticPower"].GetDouble()
            };
        }
        else
        {
            throw new Exception($"Unknown NoC type: {type}");
        }

        noc.Global = global;

        return noc;
    }

    private static GlobalSpec CreateGlobal(Dictionary<string, JsonElement> _)
    {
        return new GlobalSpec
        {
        };
    }

    public static HWSpec Load(string path)
    {
        var hwFile = JsonSerializer.Deserialize<HWFile>(File.ReadAllText(path));
        var global = CreateGlobal(hwFile.Global);
        var cores = hwFile.Cores.Select(c => CreateCoreSpec(c, hwFile.CoreTemplates, global)).ToList();

        var type = hwFile.NoC["Type"].GetString();
        var noc = CreateNoCSpec(hwFile.NoC, global);
        return new HWSpec()
        {
            Global = global,
            Cores = cores,
            NoC = noc
        };
    }

    public T FindByType<T>() where T : CoreSpec
    {
        var type = typeof(T);
        return Cores.Find(c => c.GetType() == type) as T;
    }

    public CoreSpec FindByName(string name) => Cores.Find(c => c.Name == name);
}

public class CoreSpec
{
    public GlobalSpec Global { get; set; }
    public string Name { get; set; }
    public int Priority { get; set; }
    public int MaxNeurons { get; set; }
    public int MaxSynapses { get; set; }
    public int MaxLayers { get; set; }
    public int MaxFanIn { get; set; }
    public int MaxSplits { get; set; }
    public string ConnectsTo { get; set; }
    public string[] AcceptedTypes { get; set; }
    public Dictionary<string, double> LayerCosts { get; set; }
}

public class CoreV1Spec : CoreSpec
{
    public int IntegrateDelay { get; set; }
    public int SyncDelay { get; set; }
    public double IntegrateEnergy { get; set; }
    public double SyncEnergy { get; set; }
    public double StaticPower { get; set; }
    public int NrParallel { get; set; }
    public int OutputBufferSize { get; set; }
    public bool ReportSyncEnd { get; set; }
}

public class ControllerV1Spec : CoreSpec
{
    public long StartTime { get; set; } = 0;
    public long Interval { get; set; }
    public bool GlobalSync { get; set; }
}

public class NoCSpec
{
    public GlobalSpec Global { get; set; }
}

public class MeshSpec : NoCSpec
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int InputSize { get; set; }
    public int OutputSize { get; set; }
    public int TransferDelay { get; set; }
    public int SwitchDelay { get; set; }
    public double TransferEnergy { get; set; }
    public double StaticPower { get; set; }
    public int InputDelay { get; set; }
    public int OutputDelay { get; set; }
}

public class BusSpec : NoCSpec
{
    public int Ports { get; set; }
    public int TransferDelay { get; set; }
    public double TransferEnergy { get; set; }
    public double StaticPower { get; set; }
}

public class GlobalSpec
{
    public double Frequency { get; set; } = 10_000_000.0;
}