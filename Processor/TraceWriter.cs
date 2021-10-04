using System.Text.Json;
using System.IO;
using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    class JsonTrace
    {
        public List<object> traceEvents { get; set; } = new List<object>();
    }

    class CompleteEvent
    {
        public int pid { get; } = 1;
        public int tid { get; } = 1;
        public long ts { get; set; }
        public long dur { get; set; }
        public string ph { get; } = "X";
        public string name { get; set; }
    }

    class TraceWriter
    {
        private string output;
        private StreamWriter outputFile;
        private bool started = false;
        private bool firstEvent = true;

        public TraceWriter(string output)
        {
            this.output = output;
        }

        public void WriteEvent(long start, long end, string name)
        {
            if (!started)
            {
                throw new Exception("Not running");
            }

            CompleteEvent ev = new CompleteEvent()
            {
                dur = end - start,
                ts = start,
                name = name,
            };
            if (firstEvent)
            {
                firstEvent = false;
            }
            else
            {
                outputFile.Write(",");
            }
            outputFile.Write(JsonSerializer.Serialize(ev));
        }

        public void Start()
        {
            if (started)
            {
                throw new Exception("Already started");
            }

            started = true;
            outputFile = new StreamWriter(output);
            outputFile.Write("{\"traceEvents\":[");
        }

        public void Stop()
        {
            if (!started)
            {
                throw new Exception("Not running");
            }

            outputFile.Write("]}");
            outputFile.Close();
        }
    }
}