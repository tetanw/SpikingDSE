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
    public List<Dictionary<string, JsonElement>> Cores { get; set; }
}

public class HWSpec
{
    public GlobalSpec Global { get; set; }
    public List<CoreSpec> Cores { get; set; }
    public NoCSpec NoC { get; set; }

    private static CoreSpec CreateCoreSpec(Dictionary<string, JsonElement> instance, GlobalSpec global)
    {
        string type = instance["Type"].GetString();
        if (type == "controller-v1")
        {
            return new ControllerV1Spec
            {
                Global = global,
                Name = instance["Name"].GetString(),
                Interval = instance["Interval"].GetInt64(),
                ConnectsTo = instance["ConnectsTo"].GetString(),
                MaxNeurons = int.MaxValue
            };
        }
        else if (type == "core-v1")
        {
            return new CoreV1Spec
            {
                Global = global,
                Name = instance["Name"].GetString(),
                IntegrateDelay = instance["IntegrateDelay"].GetInt32(),
                SyncDelay = instance["SyncDelay"].GetInt32(),
                ConnectsTo = instance["ConnectsTo"].GetString(),
                MaxNeurons = instance["MaxNeurons"].GetInt32(),
                IntegrateEnergy = instance["IntegrateEnergy"].GetDouble(),
                SyncEnergy = instance["SyncEnergy"].GetDouble(),
                StaticPower = instance["StaticPower"].GetDouble(),
                OutputBufferSize = instance["OutputBufferSize"].GetInt32(),
                NrParallel = instance["NrParallel"].GetInt32(),
                ReportSyncEnd = instance["ReportSyncEnd"].GetBoolean()
            };
        }
        else
        {
            throw new Exception($"Invalid instance type: {type}");
        }
    }

    private static MeshSpec CreateMesh(Dictionary<string, JsonElement> NoC, GlobalSpec global)
    {
        return new MeshSpec
        {
            Global = global,
            Width = NoC["Width"].GetInt32(),
            Height = NoC["Height"].GetInt32(),
            InputSize = NoC["InputSize"].GetInt32(),
            OutputSize = NoC["OutputSize"].GetInt32(),
            SwitchDelay = NoC["SwitchDelay"].GetInt32(),
            TransferDelay = NoC["TransferDelay"].GetInt32(),
            TransferEnergy = NoC["TransferEnergy"].GetDouble(),
            StaticPower = NoC["StaticPower"].GetDouble(),
            InputDelay = NoC["InputDelay"].GetInt32(),
            OutputDelay = NoC["OutputDelay"].GetInt32(),
        };
    }

    private static BusSpec CreateBus(Dictionary<string, JsonElement> NoC, GlobalSpec global)
    {
        return new BusSpec
        {
            Global = global,
            Ports = NoC["Ports"].GetInt32(),
            TransferDelay = NoC["TransferDelay"].GetInt32(),
            TransferEnergy = NoC["TransferEnergy"].GetDouble(),
            StaticPower = NoC["StaticPower"].GetDouble()
        };
    }

    private static GlobalSpec CreateGlobal(Dictionary<string, JsonElement> global)
    {
        return new GlobalSpec
        {
            Frequency = global["Frequency"].GetDouble()
        };
    }

    public static HWSpec Load(string path)
    {
        var hwFile = JsonSerializer.Deserialize<HWFile>(File.ReadAllText(path));
        var global = CreateGlobal(hwFile.Global);
        var cores = hwFile.Cores.Select(c => CreateCoreSpec(c, global)).ToList();

        var type = hwFile.NoC["Type"].GetString();
        NoCSpec noc;
        if (type == "Mesh")
        {
            noc = CreateMesh(hwFile.NoC, global);
        }
        else if (type == "Bus")
        {
            noc = CreateBus(hwFile.NoC, global);
        }
        else
        {
            throw new Exception($"Unknown NoC type: {type}");
        }

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
    public int MaxNeurons { get; set; }
    public string ConnectsTo { get; set; }
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
    public bool DoGlobalSync { get; set; } = false;
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
    public double Frequency { get; set; }
}