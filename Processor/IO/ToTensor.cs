using System.Collections.Generic;
using System.IO;

namespace SpikingDSE
{
    public class ToTensor
    {
        private TensorFile tensor;
        private EventTraceReader events;
        private StreamWriter sw;

        public ToTensor(string inputTensorPath, string inputEventPath, string outputTensorPath)
        {
            this.tensor = new TensorFile(inputTensorPath, -1);
            this.events = new EventTraceReader(inputEventPath);
            this.sw = new StreamWriter(File.OpenWrite(outputTensorPath));

            sw.Write(",");
            for (int i = 0; i < tensor.NrNeurons() - 1; i++)
            {
                sw.Write($"{i},");
            }
            sw.WriteLine(tensor.NrNeurons());
        }

        private void EmitOutputs(int TS, List<int> outputs)
        {
            sw.Write($"{TS}");
            for (int i = 0; i < tensor.NrNeurons(); i++)
            {
                if (outputs.Contains(i))
                {
                    sw.Write(",1.0");
                }
                else
                {
                    sw.Write(",0.0");
                }
            }
            sw.WriteLine();
        }

        public void Run()
        {
            int TS = 0;

            events.NextEvent();
            while (!tensor.NextTimestep())
            {
                var currentOutputs = new List<int>();
                var currentInputs = tensor.NeuronSpikes();

                while (!events.IsDone)
                {
                    // if we get an input spike that is not from this timestep
                    // we must be at the next timestep so we quit
                    if (events.CurrentType == EventType.InputSpike && !currentInputs.Contains(events.CurrentNeuronID))
                    {
                        events.NextEvent();
                        break;
                    }

                    // we need to register the output spikes
                    if (events.CurrentType == EventType.OutputSpike)
                    {
                        currentOutputs.Add(events.CurrentNeuronID);
                    }

                    events.NextEvent();
                }

                // handle current outputs
                EmitOutputs(TS, currentOutputs);
                TS++;
            }

            sw.Flush();
            sw.Close();
        }
    }
}