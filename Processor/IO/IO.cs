using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SpikingDSE;

public interface ISpikeSource
{
    public bool NextTimestep();
    public List<int> NeuronSpikes();
    public int NrNeurons();
    public int NrTimesteps();
}

class DatasetInfo
{
    public int InputSize { get; set; }
    public int NrSamples { get; set; }
    public int Timesteps { get; set; }
}

public class ZipDataset : IDisposable
{
    private readonly ZipArchive archive;
    private readonly Dictionary<string, ZipArchiveEntry> entries = new();
    private DatasetInfo info;

    public ZipDataset(string zipPath)
    {
        archive = ZipFile.OpenRead(zipPath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            entries[entry.Name] = entry;
        }

        ReadInfo();
    }

    private void ReadInfo()
    {
        var infoEntry = entries["info.json"];
        using var sr = new StreamReader(infoEntry.Open(), Encoding.UTF8);
        var content = sr.ReadToEnd();

        info = JsonSerializer.Deserialize<DatasetInfo>(content);
    }

    public void Dispose()
    {
        archive.Dispose();
        GC.SuppressFinalize(this);
    }

    public InputTraceFile ReadEntry(string name)
    {
        var entry = entries[name];

        return InputTraceFile.ReadFromStream(entry.Open(), info.InputSize, info.Timesteps);
    }

    public int NrSamples
    {
        get => info.NrSamples;
    }
}

public class InputTraceFile : ISpikeSource
{
    public int Correct;
    private List<List<int>> allSpikes;
    private int currentTS;
    private int nrNeurons;
    private int nrTimesteps;

    public static InputTraceFile ReadFromPath(string path, int nrNeurons, int timesteps)
    {
        var stream = File.OpenRead(path);
        var file = ReadFromStream(stream, nrNeurons, timesteps);
        stream.Dispose();
        return file;
    }

    public static InputTraceFile ReadFromStream(Stream stream, int nrNeurons, int timesteps)
    {
        var file = new InputTraceFile
        {
            allSpikes = new()
        };
        int ts = 0;
        using (StreamReader sr = new(stream))
        {
            string line = null;
            file.Correct = int.Parse(sr.ReadLine());

            while ((line = sr.ReadLine()) != null)
            {
                var parts = line.Split(",", StringSplitOptions.RemoveEmptyEntries);
                List<int> spikes = parts.Skip(1).Select(v => int.Parse(v)).ToList();
                file.allSpikes.Add(spikes);
                ts++;
            }
        }
        // If not all timesteps are defined then fill up until the required amount of timesteps
        for (int i = 0; i < timesteps - ts; i++)
        {
            file.allSpikes.Add(new());
        }
        file.currentTS = 0;
        file.nrNeurons = nrNeurons;
        file.nrTimesteps = file.allSpikes.Count;
        stream.Dispose();
        return file;
    }

    public List<int> NeuronSpikes()
    {
        return allSpikes[currentTS - 1];
    }

    public bool NextTimestep()
    {
        if (currentTS + 1 == allSpikes.Count)
        {
            return false;
        }
        else
        {
            currentTS++;
            return true;
        }
    }

    public int NrNeurons() => nrNeurons;

    public int NrTimesteps() => nrTimesteps;
}

public class TensorFile : ISpikeSource
{
    private readonly StreamReader input;
    private readonly int baseID;
    private readonly List<int> inputSpikes = new();
    private readonly int nrNeurons;
    private readonly int nrTimesteps;

    public TensorFile(string inputPath, int nrTimesteps, int baseID = 0)
    {
        this.baseID = baseID;
        this.nrTimesteps = nrTimesteps;
        this.input = new StreamReader(File.OpenRead(inputPath));

        // skip the header on both files
        string header = input.ReadLine();
        nrNeurons = int.Parse(header.Split(",").Last()) + 1;
    }

    public bool IsDone { get; private set; }

    public List<int> NeuronSpikes()
    {
        return inputSpikes;
    }

    public bool NextTimestep()
    {
        inputSpikes.Clear();
        string inputLine = input.ReadLine();
        if (inputLine == null)
        {
            return false;
        }

        int neuronIndex = baseID;
        var inputParts = inputLine.Split(",").Skip(1);
        foreach (var part in inputParts)
        {
            if (part.Equals("1.0"))
            {
                inputSpikes.Add(neuronIndex);
            }
            neuronIndex++;
        }

        return true;
    }

    public int NrNeurons() => nrNeurons;
    public int NrTimesteps() => nrTimesteps;
}

public class TensorFileGroup : ISpikeSource
{
    private readonly TensorFile[] tensorFiles;
    private bool isAnyFileDone;

    public TensorFileGroup(string[] inputFiles)
    {
        tensorFiles = new TensorFile[inputFiles.Length];
        int baseNeuronID = 0;
        for (int i = 0; i < inputFiles.Length; i++)
        {
            tensorFiles[i] = new TensorFile(inputFiles[i], baseNeuronID);
            baseNeuronID += tensorFiles[i].NrNeurons();
        }
    }

    public List<int> NeuronSpikes()
    {
        List<int> allSpikes = new();
        for (int i = 0; i < tensorFiles.Length; i++)
        {
            allSpikes.AddRange(tensorFiles[i].NeuronSpikes());
        }
        return allSpikes;
    }

    public bool NextTimestep()
    {
        if (isAnyFileDone)
            return false;

        for (int i = 0; i < tensorFiles.Length; i++)
        {
            if (!tensorFiles[i].NextTimestep())
            {
                isAnyFileDone = true;
                return false;
            }
        }

        return true;
    }

    public int[] LayerSizes()
    {
        int[] sizes = new int[tensorFiles.Length];
        for (int i = 0; i < tensorFiles.Length; i++)
        {
            sizes[i] = tensorFiles[i].NrNeurons();
        }
        return sizes;
    }

    public int NrNeurons() => tensorFiles.Sum((tf) => tf.NrNeurons());

    public int NrTimesteps() => tensorFiles[0].NrNeurons();
}

public enum EventType
{
    InputSpike,
    OutputSpike,
    None
}

public class EventTraceReader
{
    public static IEnumerable<int> ReadInputs(string path)
    {
        var reader = new EventTraceReader(path, 1);
        while (reader.NextEvent())
        {
            if (reader.CurrentType == EventType.InputSpike)
            {
                yield return reader.CurrentNeuronID;
            }
        }
    }

    private readonly StreamReader file;
    private string line;
    private readonly long clkPeriod;

    public EventTraceReader(string filePath, int frequency = int.MaxValue)
    {
        file = new StreamReader(File.OpenRead(filePath));
        clkPeriod = 1_000_000_000_000 / frequency;
    }

    public bool NextEvent()
    {
        CurrentType = EventType.None;

        line = file.ReadLine();
        if (line == null)
        {
            return false;
        }

        var parts = line.Split(",");
        if (parts[0].Equals("0"))
        {
            // new input spike event
            CurrentNeuronID = int.Parse(parts[1]);
            long time = long.Parse(parts[2]);
            CurrentTime = time / clkPeriod;
            CurrentType = EventType.InputSpike;
        }
        else if (parts[0].Equals("1"))
        {
            // new output spike vent
            CurrentNeuronID = int.Parse(parts[1]);
            long time = long.Parse(parts[2]);
            CurrentTime = time / clkPeriod;
            CurrentType = EventType.OutputSpike;
        }

        return true;
    }

    public bool IsDone => line == null;
    public EventType CurrentType { get; private set; }
    public int CurrentNeuronID { get; private set; }
    public long CurrentTime { get; private set; }
}