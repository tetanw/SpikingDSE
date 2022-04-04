using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpikingDSE;

class HWFile
{
    public Dictionary<string, JsonElement> NoC { get; set; }
    public List<Dictionary<string, JsonElement>> Cores { get; set; }
}

public class HWSpec
{
    public List<CoreSpec> Cores { get; set; }
    public NoCSpec NoC { get; set; }

    private static CoreSpec CreateCoreSpec(Dictionary<string, JsonElement> instance)
    {
        string type = instance["Type"].GetString();
        if (type == "controller-v1")
        {
            return new ControllerV1Spec
            {
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
                Name = instance["Name"].GetString(),
                IntegrateDelay = instance["IntegrateDelay"].GetInt32(),
                SyncDelay = instance["SyncDelay"].GetInt32(),
                ConnectsTo = instance["ConnectsTo"].GetString(),
                MaxNeurons = instance["MaxNeurons"].GetInt32(),
                ComputeEnergy = instance["ComputeEnergy"].GetDouble(),
                ComputeBufferSize = instance["ComputeBufferSize"].GetInt32(),
                OutputBufferSize = instance["OutputBufferSize"].GetInt32(),
                NrParallel = instance["NrParallel"].GetInt32(),
                IgnoreLayers = instance["IgnoreLayers"].GetBoolean()
            };
        }
        else
        {
            throw new System.Exception($"Invalid instance type: {type}");
        }
    }

    private static MeshSpec BuildMesh(Dictionary<string, JsonElement> NoC)
    {
        return new MeshSpec
        {
            Width = NoC["Width"].GetInt32(),
            Height = NoC["Height"].GetInt32(),
            InputSize = NoC["InputSize"].GetInt32(),
            OutputSize = NoC["OutputSize"].GetInt32(),
            SwitchDelay = NoC["SwitchDelay"].GetInt32(),
            TransferDelay = NoC["TransferDelay"].GetInt32(),
            TransferEnergy = NoC["TransferEnergy"].GetDouble(),
            StaticEnergy = NoC["StaticEnergy"].GetDouble(),
            Frequency = NoC["Frequency"].GetDouble(),
            InputDelay = NoC["InputDelay"].GetInt32(),
            OutputDelay = NoC["OutputDelay"].GetInt32(),
        };
    }

    private static BusSpec BuildBus(Dictionary<string, JsonElement> NoC)
    {
        return new BusSpec
        {
            Ports = NoC["Ports"].GetInt32(),
            TransferDelay = NoC["TransferDelay"].GetInt32()
        };
    }

    public static HWSpec Load(string path)
    {
        var hwFile = JsonSerializer.Deserialize<HWFile>(File.ReadAllText(path));

        var cores = hwFile.Cores.Select(c => CreateCoreSpec(c)).ToList();

        var type = hwFile.NoC["Type"].GetString();
        NoCSpec noc;
        if (type == "Mesh")
        {
            noc = BuildMesh(hwFile.NoC);
        }
        else if (type == "Bus")
        {
            noc = BuildBus(hwFile.NoC);
        }
        else
        {
            throw new Exception($"Unknown NoC type: {type}");
        }

        return new HWSpec()
        {
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
    public string Name { get; set; }
    public int MaxNeurons { get; set; }
    public string ConnectsTo { get; set; }
}

public class CoreV1Spec : CoreSpec
{
    public int IntegrateDelay { get; set; }
    public int SyncDelay { get; set; }
    public double ComputeEnergy { get; set; }
    public int NrParallel { get; set; }
    public int OutputBufferSize { get; set; }
    public int ComputeBufferSize { get; set; }
    public bool IgnoreLayers { get; set; }
}

public class ControllerV1Spec : CoreSpec
{
    public long StartTime { get; set; } = 0;
    public long Interval { get; set; }
}

public class NoCSpec
{
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
    public double StaticEnergy { get; set; }
    public double Frequency { get; set; }
    public int InputDelay { get; set; }
    public int OutputDelay { get; set; }
}

public class BusSpec : NoCSpec
{
    public int Ports { get; set; }
    public int TransferDelay { get; set; }
}