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
    }

    public class TensorFile : ISpikeSource
    {
        private StreamReader input;
        private int baseID;
        private List<int> inputSpikes = new List<int>();

        public TensorFile(string inputPath, int baseID = 0)
        {
            this.baseID = baseID;
            this.input = new StreamReader(File.OpenRead(inputPath));

            // skip the header on both files
            string header = input.ReadLine();
            NrNeurons = int.Parse(header.Split(",").Last()) + 1;
        }

        public bool IsDone { get; private set; }

        public int NrNeurons { get; private set; }

        public List<int> NeuronSpikes()
        {
            return inputSpikes;
        }

        public bool NextTimestep()
        {
            inputSpikes.Clear();
            string inputLine = input.ReadLine();
            int partIndex;

            if (IsDone || inputLine == null)
            {
                IsDone = true;
                return true;
            }

            partIndex = baseID;
            var inputParts = inputLine.Split(",").Skip(1);
            foreach (var part in inputParts)
            {
                if (part.Equals("1.0"))
                {
                    inputSpikes.Add(partIndex);
                }
                partIndex++;
            }

            return false;
        }
    }

    public class TensorFileGroup : ISpikeSource
    {
        private TensorFile[] tensorFiles;
        private bool isDone;

        public TensorFileGroup(string[] inputFiles)
        {
            tensorFiles = new TensorFile[inputFiles.Length];
            int baseNeuronID = 0;
            for (int i = 0; i < inputFiles.Length; i++)
            {
                tensorFiles[i] = new TensorFile(inputFiles[i], baseNeuronID);
                baseNeuronID += tensorFiles[i].NrNeurons;
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
            if (isDone)
                return true;

            for (int i = 0; i < tensorFiles.Length; i++)
            {
                bool isFileDone = tensorFiles[i].NextTimestep();
                if (isFileDone) {
                    isDone = true;
                    break;
                }
            }

            return false;
        }

        public int[] LayerSizes()
        {
            int[] sizes = new int[tensorFiles.Length];
            for (int i = 0; i < tensorFiles.Length; i++)
            {
                sizes[i] = tensorFiles[i].NrNeurons;
            }
            return sizes;
        }
    }

    public enum EventType
    {
        InputSpike,
        OutputSpike,
        None
    }

    public class TraceReader
    {
        private StreamReader file;
        private string line;

        public TraceReader(string filePath)
        {
            this.file = new StreamReader(File.OpenRead(filePath));
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
                CurrentType = EventType.InputSpike;
            }
            else if (parts[0].Equals("1"))
            {
                // new output spike vent
                CurrentNeuronID = int.Parse(parts[1]);
                CurrentType = EventType.OutputSpike;
            }

            return true;
        }

        public bool IsDone => line == null;

        public EventType CurrentType { get; private set; }

        public int CurrentNeuronID { get; private set; }
    }

    public enum TraceFileMode
    {
        InputsOnly,
        OutputsOnly,
        InputsAndOutputs
    }

    public class TraceFile : ISpikeSource
    {
        private TraceFileMode mode;
        private TraceReader reader;
        private List<int> spikes;

        public TraceFile(string filePath, TraceFileMode mode)
        {
            this.mode = mode;
            this.reader = new TraceReader(filePath);
        }

        public List<int> NeuronSpikes()
        {
            return spikes;
        }

        public bool NextTimestep()
        {
            spikes = new List<int>();

            bool doReadInputs = mode == TraceFileMode.InputsOnly || mode == TraceFileMode.InputsAndOutputs;
            bool doReadOutputs = mode == TraceFileMode.OutputsOnly || mode == TraceFileMode.InputsAndOutputs;

            while (reader.NextEvent())
            {
                if (reader.CurrentType == EventType.InputSpike && doReadInputs)
                {
                    spikes.Add(reader.CurrentNeuronID);
                }
                else if (reader.CurrentType == EventType.OutputSpike && doReadOutputs)
                {
                    spikes.Add(reader.CurrentNeuronID);
                }
            }

            return false;
        }
    }


    public class SpikeBuffer
    {
        private ISpikeSource source;
        private bool isDone;
        private List<int> neuronSpikes;

        public SpikeBuffer(ISpikeSource source)
        {
            this.source = source;

            NextTimestep();
        }

        public int PopNeuronSpike()
        {
            int neuronSpike = neuronSpikes[0];
            neuronSpikes.RemoveAt(0);
            return neuronSpike;
        }

        public void NextTimestep()
        {
            if (neuronSpikes != null && neuronSpikes.Count > 0)
            {
                throw new Exception("Still spikes left");
            }

            this.isDone = source.NextTimestep();
            neuronSpikes = new List<int>(source.NeuronSpikes());

            TS++;
        }

        public int TS { get; private set; } = -1;

        public bool IsDone
        {
            get => isDone;
        }

        public bool IsEmpty
        {
            get => neuronSpikes.Count == 0;
        }
    }
}