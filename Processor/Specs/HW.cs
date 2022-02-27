using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;
using System;

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

    private static CoreSpec CreateCoreSpec(HWFile file, Dictionary<string, JsonElement> instance)
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
                BufferSize = instance["BufferSize"].GetInt32(),
                ComputeDelay = instance["ComputeDelay"].GetInt32(),
                InputDelay = instance["InputDelay"].GetInt32(),
                OutputDelay = instance["OutputDelay"].GetInt32(),
                ConnectsTo = instance["ConnectsTo"].GetString(),
                MaxNeurons = instance["MaxNeurons"].GetInt32(),
                ComputeEnergy = instance["ComputeEnergy"].GetDouble()
            };
        }
        else
        {
            throw new System.Exception($"Invalid instance type: {type}");
        }
    }

    public static HWSpec Load(string path)
    {
        var hwFile = JsonSerializer.Deserialize<HWFile>(File.ReadAllText(path));

        var cores = hwFile.Cores.Select(c => CreateCoreSpec(hwFile, c)).ToList();

        return new HWSpec()
        {
            Cores = cores,
            NoC = new MeshSpec {
                Width = hwFile.NoC["Width"].GetInt32(),
                Height = hwFile.NoC["Height"].GetInt32(),
                InputSize = hwFile.NoC["InputSize"].GetInt32(),
                OutputSize = hwFile.NoC["OutputSize"].GetInt32(),
                ReswitchDelay = hwFile.NoC["ReswitchDelay"].GetInt32(),
                PacketRouteDelay = hwFile.NoC["PacketRouteDelay"].GetInt32(),
                TransferDelay = hwFile.NoC["TransferDelay"].GetInt32(),
                TransferEnergy = hwFile.NoC["TransferEnergy"].GetDouble()
            }
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
    public int BufferSize { get; set; }
    public int InputDelay { get; set; }
    public int OutputDelay { get; set; }
    public int ComputeDelay { get; set; }
    public double ComputeEnergy { get; set; }
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
    public int ReswitchDelay { get; set; }
    public int PacketRouteDelay { get; set; }
    public double TransferEnergy { get; set; }
}