using System;
using System.Collections.Generic;
using System.IO;

namespace SpikingDSE
{
    public class VCDProcessor
    {
        protected long time;
        protected string input;

        public VCDProcessor(string input)
        {
            this.input = input;
        }

        public virtual void OnValueChange(string id, long newValue) {}

        public virtual void OnComplete() {}

        private void ParseScalar(string id, string value)
        {
            if (value != "0" && value != "1")
            {
                return;
            }

            OnValueChange(id, long.Parse(value));
        }

        private void ParseVector(string format, string id, string value)
        {
            if (format == "b" || format == "B")
            {
                try
                {

                    if (!value.Contains("x") && value.Length < 64)
                    {
                        OnValueChange(id, Convert.ToInt64(value, 2));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to parse {value}: {e.Message}");
                    return;
                }
            }
        }

        private bool isScalar(string c)
        {
            // 01xXzZ
            return
                c.Equals("0") ||
                c.Equals("1") ||
                c.Equals("x") ||
                c.Equals("X") ||
                c.Equals("z") ||
                c.Equals("Z");
        }

        private bool isVector(string c)
        {
            // bBrR
            return
                c.Equals("b") ||
                c.Equals("B") ||
                c.Equals("r") ||
                c.Equals("R");
        }

        private void ParseValueChangeLine(string line)
        {
            string c = line.Substring(0, 1);
            string rest = line.Substring(1);
            if (isScalar(c))
            {
                ParseScalar(id: rest, value: c);
            }
            else if (isVector(c))
            {
                string[] parts = line.Split(" ");
                string id = parts[1];
                string format = parts[0].Substring(0, 1);
                string value = parts[0].Substring(1);
                ParseVector(format, id, value);
            }
        }

        public void Process(long maxLinesRead = long.MaxValue)
        {
            var vcd = new StreamReader(input);

            string line;
            while ((line = vcd.ReadLine()) != null)
            {
                if (line == "$enddefinitions $end")
                {
                    break;
                }
            }

            while ((line = vcd.ReadLine()) != null)
            {
                if (line == "$end")
                {
                    break;
                }
            }

            time = -1;
            long linesRead = 0;
            while ((line = vcd.ReadLine()) != null && ++linesRead < maxLinesRead && line != "$dumpoff")
            {
                if (line.StartsWith("#"))
                {
                    line = line.Replace("#", "");
                    time = long.Parse(line);
                    continue;
                }
                else if (line == "$end")
                {
                    // binary we are done
                    break;
                }

                ParseValueChangeLine(line);
            }

            OnComplete();
            Console.WriteLine($"{linesRead} lines read");
        }
    }

    class FullSpike
    {
        public long HWTime { get; set; }
        public long Synapse { get; set; }
    }

    class TraceNeuronVCD : VCDProcessor, IDisposable
    {
        private StreamWriter sw;
        private FullSpike spike;
        private List<long> spikeBuffer;

        public TraceNeuronVCD(string input, string output) : base (input)
        {
            sw = new StreamWriter(output);
            spike = new FullSpike();
            spikeBuffer = new List<long>();
        }

        public override void OnValueChange(string id, long newValue)
        {
            // P  -> CTRL_NEURMEM_ADDR -> synapse being handled
            // 9  -> AEROUT_CTRL_BUSY  -> output event generated
            // <" -> CTRL_SCHED_ADDR   -> neuron on input
            // [" -> state             -> current state

            // when a new neuron event is added
            if (id == "<\"" && newValue != 0)
            {
                spikeBuffer.Add(newValue);
            }

            // when the next synapse is handled
            if (id == "P" && newValue != 0)
            {
                spike.HWTime = time;
                spike.Synapse = newValue;
            }

            // when a new neuron starts being computed
            if (id == "[\"" && newValue == 9)
            {
                long currentNeuron = spikeBuffer[0];
                spikeBuffer.RemoveAt(0);
                sw.Write($"0,{currentNeuron},{time}\n");
            }

            // when an output spike is generated
            if (id == "9" && newValue == 1)
            {
                sw.Write($"1,{spike.Synapse - 1},{time}\n");
            }
        }

        public override void OnComplete()
        {
            sw.Flush();
        }

        public void Dispose()
        {
            sw.Dispose();
        }
    }

}