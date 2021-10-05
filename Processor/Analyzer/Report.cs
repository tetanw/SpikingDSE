using System.Collections.Generic;

namespace SpikingDSE
{
    public class CoreReport
    {
        public CoreReport(int coreID, int TS)
        {
            this.CoreID = coreID;
            this.TS = TS;
        }

        public int CoreID { get; private set; }
        public int TS { get; private set; }

        public SpikeMap Spikes { get; set; }
        public Memory Memory { get; set; }
        public Latency Latency { get; set; }
        public Energy Energy { get; set; }

    }


    public class TimestepReport
    {
        public TimestepReport(int TS)
        {
            this.TS = TS;
        }
        public int TS { get; private set; }

        public Latency Latency { get; set; } = new Latency();
        public Energy Energy { get; set; } = new Energy();
        public List<SpikeRoute> SpikeRoutes { get; set; }
        public List<CoreReport> Cores { get; set; } = new List<CoreReport>();
    }

    public class SimReport
    {
        public HWConfig HW { get; set; }
        public CostConfig Cost { get; set; }

        public Latency Latency { get; set; } = new Latency();
        public Energy Energy { get; set; } = new Energy();

        public MappingReport Mapping { get; set; }
        public List<TimestepReport> Timesteps { get; set; } = new List<TimestepReport>();
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
}