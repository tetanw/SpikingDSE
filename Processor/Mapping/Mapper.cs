using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public class MappedLayer
{
    public string Layer { get; set; }
    public string Core { get; set; }
    public bool Partial { get; set; }
    public int Index { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
}

public class Mapping
{
    public List<MappedLayer> Mapped { get; set; } = new();
    public List<string> Unmapped { get; set; } = new();

    public void PrintReport()
    {
        Console.WriteLine("Mappings:");
        foreach (var entry in Mapped)
        {
            if (entry.Partial)
            {
                Console.WriteLine($"  {entry.Layer} from {entry.Start} to {entry.End} -> {entry.Core}");
            }
            else
            {
                Console.WriteLine($"  {entry.Layer} -> {entry.Core}");
            }
        }
        Console.WriteLine("Unmapped:");
        foreach (var layerName in Unmapped)
        {
            Console.WriteLine($"  {layerName}");
        }
    }

    public IEnumerable<MappedLayer> GetAllSplits(string name)
    {
        return Mapped.FindAll((m) => m.Layer == name);
    }

    public void Save(string path)
    {
        using var fileStream = File.Create(path);
        JsonSerializer.Serialize(fileStream, this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static Mapping Load(string path)
    {
        using var fileStream = File.Open(path, FileMode.Open);
        return JsonSerializer.Deserialize<Mapping>(fileStream);
    }
}

public abstract class Mapper
{
    protected readonly HWSpec hw;
    protected readonly SNN snn;

    public Mapper(HWSpec hw, SNN snn)
    {
        this.hw = hw;
        this.snn = snn;
    }

    public abstract Mapping Run();
}