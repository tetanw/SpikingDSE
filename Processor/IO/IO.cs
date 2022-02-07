using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpikingDSE
{
    public interface ISpikeSource
    {
        public bool NextTimestep();
        public List<int> NeuronSpikes();
        public int NrNeurons();
        public int NrTimesteps();
    }

    class InputTraceFile : ISpikeSource
    {
        public int Correct;
        private List<List<int>> allSpikes;
        private int currentTS;
        private int nrNeurons;
        private string inputPath;
        private int nrTimesteps;

        public InputTraceFile(string inputPath, int nrNeurons, int nrTimesteps)
        {
            this.inputPath = inputPath;
            string[] lines = File.ReadAllLines(inputPath);
            Correct = int.Parse(lines[0]);
            allSpikes = new();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                var parts = line.Split(",", StringSplitOptions.RemoveEmptyEntries);
                List<int> spikes = parts.Skip(1).Select(v => int.Parse(v)).ToList();
                allSpikes.Add(spikes);
            }
            currentTS = 0;
            this.nrNeurons = nrNeurons;
            this.nrTimesteps = nrTimesteps;
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
        private StreamReader input;
        private int baseID;
        private List<int> inputSpikes = new List<int>();
        private int nrNeurons;
        private int nrTimesteps;

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
        private TensorFile[] tensorFiles;
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
            List<int> allSpikes = new List<int>();
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

        private StreamReader file;
        private string line;
        private long clkPeriod;

        public EventTraceReader(string filePath, int frequency = int.MaxValue)
        {
            this.file = new StreamReader(File.OpenRead(filePath));
            this.clkPeriod = 1_000_000_000_000 / frequency;
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
}