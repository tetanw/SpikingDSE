using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpikingDSE
{
    public class PEConfig
    {
        public PEConfig()
        {
            this.MemNeuron = new MemoryConfig();
            this.MemSynapse = new MemoryConfig();
            this.Input = new CommConfig();
            this.Output = new CommConfig();
            this.Core = new CoreConfig();
        }

        public int MaxNeurons { get; set; }
        public int MaxSynapses { get; set; }
        public int Frequency { get; set; }
        public int BufferSize { get; set; }

        public MemoryConfig MemNeuron { get; set; }
        public MemoryConfig MemSynapse { get; set; }
        public CommConfig Input { get; set; }
        public CommConfig Output { get; set; }
        public CoreConfig Core { get; set; }
        public ControllerConfig Controller { get; set; }

        public static PEConfig LoadConfig(string configPath)
        {
            using FileStream openStream = File.OpenRead(configPath);
            var config = JsonSerializer.DeserializeAsync<PEConfig>(openStream).Result;

            return config;
        }

        public void Save(string configPath)
        {
            using FileStream openStream = File.OpenWrite(configPath);
            JsonSerializer.SerializeAsync(openStream, this, new JsonSerializerOptions() { WriteIndented = true }).Wait();
        }
    }

    public class MemoryConfig
    {
        public int Latency { get; set; }
        public int BatchSize { get; set; }
        public double ReadEnergy { get; set; }
        public double WriteEnergy { get; set; }
        public double Leakage { get; set; }
    }

    public class CommConfig
    {
        public long Latency { get; set; }
    }

    public class CoreConfig
    {
        public long PrepTime { get; set; }
        public int ComputeTime { get; set; }
        public int MaxParallelism { get; set; }
    }

    public class ControllerConfig
    {
        public double Leakage { get; set; }
        public double EnergyPerEvent { get; set; }
    }

}