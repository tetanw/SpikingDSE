using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

class HWFile
{
    public Dictionary<string, JsonElement> NoC { get; set; }
    public Dictionary<string, JsonElement> CoreTemplates { get; set; }
    public List<Dictionary<string, JsonElement>> Cores { get; set; }
}

public class HWSpec
{
    public List<CoreSpec> Cores { get; set; }
    public NoCSpec NoC { get; set; }

    private static CoreSpec CreateCoreSpec(Dictionary<string, JsonElement> instance, Dictionary<string, JsonElement> templates)
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
                IgnoreIdleCores = instance["IgnoreIdleCores"].GetBoolean(),
                SyncDelay = instance.GetOptional("SyncDelay")?.GetInt64() ?? 0
            };
        }
        else if (type == "core-v1")
        {
            var coreV1Spec = new CoreV1Spec
            {
                ConnectsTo = instance["ConnectsTo"].GetString(),
                MaxNeurons = instance["MaxNeurons"].GetInt32(),
                NrParallel = instance["NrParallel"].GetInt32(),
                ReportSyncEnd = instance["ReportSyncEnd"].GetBoolean(),
                OutputBufferDepth = instance["OutputBufferDepth"].GetInt32(),
                DisableIfIdle = instance["DisableIfIdle"].GetBoolean(),
                ShowLayerStats = instance.GetOptional("ShowLayerStats")?.GetBoolean() ?? false,
                ShowMemStats = instance.GetOptional("ShowMemStats")?.GetBoolean() ?? false,
                ShowALUStats = instance.GetOptional("ShowALUStats")?.GetBoolean() ?? false,
            };
            coreV1Spec.LayerCosts = new();
            if (instance.ContainsKey("LayerCosts"))
            {
                foreach (var pair in instance["LayerCosts"].EnumerateObject())
                {
                    string layerName = pair.Name;
                    var values = pair.Value;
                    var costs = new LayerCost
                    {
                        IntegrateLat = values.GetProperty("IntegrateLat").GetInt32(),
                        IntegrateII = values.GetProperty("IntegrateII").GetInt32(),
                        SyncLat = values.GetProperty("SyncLat").GetInt32(),
                        SyncII = values.GetProperty("SyncII").GetInt32()
                    };
                    coreV1Spec.LayerCosts[layerName] = costs;
                }
            }
            core = coreV1Spec;
        }
        else
        {
            throw new Exception($"Invalid instance type: {type}");
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

    private static NoCSpec CreateNoCSpec(Dictionary<string, JsonElement> instance)
    {
        string type = instance["Type"].GetString();
        NoCSpec noc;
        if (type == "XYMesh")
        {
            noc = new XYSpec
            {
                Width = instance["Width"].GetInt32(),
                Height = instance["Height"].GetInt32(),
                InputSize = instance["InputSize"].GetInt32(),
                OutputSize = instance["OutputSize"].GetInt32(),
                SwitchDelay = instance["SwitchDelay"].GetInt32(),
                TransferDelay = instance["TransferDelay"].GetInt32(),
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
            };
        }
        else
        {
            throw new Exception($"Unknown NoC type: {type}");
        }

        return noc;
    }

    public static HWSpec Load(string path)
    {
        var hwFile = JsonSerializer.Deserialize<HWFile>(File.ReadAllText(path));
        var cores = hwFile.Cores.Select(c => CreateCoreSpec(c, hwFile.CoreTemplates)).ToList();

        var type = hwFile.NoC["Type"].GetString();
        var noc = CreateNoCSpec(hwFile.NoC);
        return new HWSpec()
        {
            Cores = cores,
            NoC = noc
        };
    }
}

public abstract class CoreSpec
{
    public string Name { get; set; }
    public int Priority { get; set; }
    public int MaxNeurons { get; set; }
    public int MaxSynapses { get; set; }
    public int MaxLayers { get; set; }
    public int MaxFanIn { get; set; }
    public int MaxSplits { get; set; }
    public string ConnectsTo { get; set; }
    public string[] AcceptedTypes { get; set; }

    public abstract Core Build();
}

public class LayerCost
{
    public int SyncII { get; set; }
    public int SyncLat { get; set; }
    public int IntegrateII { get; set; }
    public int IntegrateLat { get; set; }
    public double SyncEnergy { get; set; }
    public double IntegrateEnergy { get; set; }
}

public class CoreV1Spec : CoreSpec
{
    public int NrParallel { get; set; }
    public int OutputBufferDepth { get; set; }
    public bool ReportSyncEnd { get; set; }
    public bool DisableIfIdle { get; set; }
    public bool ShowLayerStats { get; set; }
    public bool ShowMemStats { get; set; }
    public bool ShowALUStats { get; set; }
    public Dictionary<string, LayerCost> LayerCosts { get; set; }

    public override Core Build()
    {
        return new CoreV1(this);
    }
}

public class ControllerV1Spec : CoreSpec
{
    public long StartTime { get; set; } = 0;
    public long Interval { get; set; }
    public bool GlobalSync { get; set; }
    public bool IgnoreIdleCores { get; set; }
    public long SyncDelay { get; set; }
    public int SpikeSendDelay { get; set; }

    public override Core Build()
    {
        return new ControllerV1(this);
    }
}

public abstract class NoCSpec
{
    public abstract Comm Build(Simulator env, List<Core> cores);
    public abstract object ToCoord(string connection);
}

public abstract class MeshSpec : NoCSpec
{
    public int Width { get; set; }
    public int Height { get; set; }

    public override object ToCoord(string connection)
    {
        var parts = connection.Split(",");
        var x = int.Parse(parts[0]);
        var y = int.Parse(parts[1]);
        return new MeshCoord(x, y);
    }
}

public class XYSpec : MeshSpec
{
    public int InputSize { get; set; }
    public int OutputSize { get; set; }
    public int TransferDelay { get; set; }
    public int SwitchDelay { get; set; }
    public int InputDelay { get; set; }
    public int OutputDelay { get; set; }

    public override Comm Build(Simulator env, List<Core> cores)
    {
        return new MeshComm(env, Width, Height, cores, (x, y) => new XYRouter(x, y, this));
    }
}

public class VCSpec : MeshSpec
{
    public override Comm Build(Simulator env, List<Core> cores)
    {
        throw new NotImplementedException();
    }
}

public class BusSpec : NoCSpec
{
    public int Ports { get; set; }
    public int TransferDelay { get; set; }

    public override Comm Build(Simulator env, List<Core> cores)
    {
        return new BusComm(env, this);
    }

    public override object ToCoord(string connection)
    {
        var parts = connection.Split(",");
        var id = int.Parse(parts[0]);
        return id;
    }
}