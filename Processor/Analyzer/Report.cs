using System.Collections.Generic;

namespace SpikingDSE
{
    public class PEReport
    {
        public PEReport(int coreID, int TS, SpikeMap spikes, Memory memory, Latency latency, Energy energy)
        {
            this.CoreID = coreID;
            this.TS = TS;
            Type = "PE";

            this.Spikes = spikes;
            this.Memory = memory;
            this.Latency = latency;
            this.Energy = energy;
        }

        public int CoreID { get; private set; }
        public int TS { get; private set; }
        public string Type { get; private set; }

        public SpikeMap Spikes { get; private set; }
        public Memory Memory { get; private set; }
        public Latency Latency { get; private set; }
        public Energy Energy { get; private set; }

    }


    public class TimestepReport
    {
        public TimestepReport(int TS)
        {
            this.TS = TS;
            this.Type = "Timestep";
        }

        public string Type { get; private set; }
        public int TS { get; private set; }

        public Latency Latency { get; set; } = new Latency();
        public Energy Energy { get; set; } = new Energy();
        public List<SpikeRoute> SpikeRoutes { get; set; }
    }

    public class SimReport
    {
        public SimReport()
        {
            this.Type = "Sim";
        }

        public string Type { get; private set; }

        public Latency Latency { get; set; } = new Latency();
        public Energy Energy { get; set; } = new Energy();
    }

    public class MappingReport
    {
        public MappingReport()
        {
            Type = "Mapping";
        }

        public string Type { get; set; }
        public int[] Mapping { get; set; }
    }

    public class Analysis
    {
        public List<object> Reports { get; set; } = new List<object>();
    }
}