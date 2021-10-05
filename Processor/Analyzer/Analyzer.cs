using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace SpikingDSE
{
    public class Analyzer
    {
        private TensorFileGroup input;
        private HWConfig hw;
        private SNNConfig snnConf;
        private CostConfig cost;
        private Mapper mapper;
        private SNN snn;
        private string strategyStr;

        public Analyzer(string snnPath, string hwPath, string costPath, string strategyStr)
        {
            this.hw = JsonSerializer.Deserialize<HWConfig>(File.ReadAllText(hwPath));
            this.snnConf = JsonSerializer.Deserialize<SNNConfig>(File.ReadAllText(snnPath));
            this.cost = JsonSerializer.Deserialize<CostConfig>(File.ReadAllText(costPath));

            // the creation of the mapper will be done later
            this.strategyStr = strategyStr;
        }

        private void InitMapping()
        {
            if (strategyStr.Equals("round-robin"))
            {
                mapper = new RoundRobinMapper(snn, hw);
            }
            else if (strategyStr.Equals("first-fit"))
            {
                mapper = new FirstFitMapper(snn, hw);
            }
            else
            {
                throw new Exception($"Unknown mapping strategy: {strategyStr}");
            }

            Console.WriteLine($"Constructing mapping using {mapper.Name}");
            mapper.ConstructMapping();
        }

        private SpikeMap[] GetCoreSpikeMap(List<SpikeRoute> spikeRoutes)
        {
            var coreSpikes = new SpikeMap[hw.NrPEs];
            for (int i = 0; i < hw.NrPEs; i++)
            {
                coreSpikes[i] = new SpikeMap();
            }

            foreach (var route in spikeRoutes)
            {
                // only one core has to output
                // the core from can be -1 due to coming from the AER in this
                // case do not contribute it to any core
                if (route.CoreFrom >= 0)
                {
                    coreSpikes[route.CoreFrom].Output.Add(route.NeuronID);
                }

                // multiple cores can be input
                foreach (var toCore in route.CoreTos)
                {
                    if (toCore == -1)
                    {
                        continue;
                    }

                    if (route.CoreFrom != toCore)
                    {
                        coreSpikes[toCore].Input.Add(route.NeuronID);

                    }
                    else
                    {
                        coreSpikes[toCore].Internal.Add(route.NeuronID);
                    }
                }
            }

            return coreSpikes;
        }

        private void InitSNN()
        {
            Console.WriteLine("Initializing SNN using config");

            snn = new SNN();
            string[] paths = new string[snnConf.Layers.Length];
            int i = 0;
            foreach (var layerConf in snnConf.Layers)
            {
                snn.AddLayer(layerConf.Name, layerConf.Size, layerConf.Input, layerConf.Output);
                paths[i++] = layerConf.Path;
            }
            this.input = new TensorFileGroup(paths);
        }

        private CoreReport CalculateCore(int coreID, int ts, SpikeMap spikes)
        {
            int NrSOPs = hw.NrNeurons * (spikes.Input.Count + spikes.Internal.Count);

            // Memory
            var memory = new Memory();
            memory.NeuronReads = NrSOPs / hw.MemNeuronBatchSize;
            memory.NeuronWrites = NrSOPs / hw.MemNeuronBatchSize;
            memory.SynReads = NrSOPs / hw.MemSynapseBatchSize;
            memory.SynWrites = NrSOPs / hw.MemSynapseBatchSize;

            int pipeII = 0, pipeLat = 0;
            if (hw.PipelineII != 0 && hw.PipelineLat != 0)
            {
                pipeII = hw.PipelineII;
                pipeLat = hw.PipelineLat;
            }
            else
            {
                throw new Exception("Can not determine pipeline latency and II");
            }

            // Latency
            var latency = new Latency();
            latency.Input = hw.InputLatency * spikes.Input.Count;
            latency.Output = hw.OutputLatency * spikes.Output.Count;
            latency.Compute = pipeII * NrSOPs + (spikes.Input.Count + spikes.Internal.Count) * (pipeLat - pipeII);
            latency.Internal = hw.InternalLatency * spikes.Internal.Count;

            // Energy
            var energy = new Energy();

            double timeActive = (double)latency.Total / hw.Frequency;
            energy.Core = new EnergyMetric(
                leakage: timeActive * hw.NrCores * cost.CoreLeakage,
                dynamic: NrSOPs * cost.CoreDynamic
            );

            energy.Router = new EnergyMetric(
                leakage: timeActive * cost.RouterLeakage,
                dynamic: (spikes.Input.Count + spikes.Output.Count) * cost.RouterDynamic
            );

            energy.Scheduler = new EnergyMetric(
                leakage: timeActive * hw.SchedulerBufferSize * cost.BufferLeakage,
                dynamic: (spikes.Input.Count + spikes.Internal.Count) * hw.SchedulerBufferSize * cost.BufferDynamic
            );

            energy.Controller = new EnergyMetric(
                leakage: timeActive * cost.ControllerLeakage,
                dynamic: 0.0
            );

            energy.NeuronMem = new EnergyMetric(
                leakage: timeActive * cost.MemNeuronLeakage,
                dynamic: memory.NeuronReads * cost.MemNeuronReadEnergy
                + memory.NeuronWrites * cost.MemNeuronWriteEnergy
            );

            energy.SynMem = new EnergyMetric(
                leakage: timeActive * cost.MemSynapseLeakage,
                dynamic: memory.SynReads * cost.MemSynReadEnergy
                + memory.SynWrites * cost.MemSynWriteEnergy
            );

            return new CoreReport(coreID, ts)
            {
                Memory = memory,
                Latency = latency,
                Energy = energy,
                Spikes = spikes
            };
        }

        public void Run(int maxTimesteps)
        {
            if (hw.PipelineLat < hw.PipelineII)
            {
                throw new Exception("Latency must at least be the II");
            }

            InitSNN();
            InitMapping();

            Console.WriteLine("Start analyzing");
            var simReport = new SimReport()
            {
                HW = hw,
                Cost = cost
            };
            simReport.Mapping = new MappingReport()
            {
                Mapping = mapper.coreTable
            };
            var sim = new SimReport();
            int ts = 0;
            while (!input.NextTimestep() && ts < maxTimesteps)
            {
                var allSpikes = input.NeuronSpikes();
                var allSpikeRoutes = mapper.GetAllSpikeRoutes(allSpikes);
                var coreSpikeMap = GetCoreSpikeMap(allSpikeRoutes);

                var timestep = new TimestepReport(ts);
                simReport.Timesteps.Add(timestep);
                timestep.SpikeRoutes = allSpikeRoutes;
                for (int i = 0; i < hw.NrPEs; i++)
                {
                    var coreSpikes = coreSpikeMap[i];
                    var core = CalculateCore(i, ts, coreSpikes);
                    timestep.Cores.Add(core);
                    timestep.Energy += core.Energy;
                    timestep.Latency += core.Latency;
                }
                sim.Energy += timestep.Energy;
                sim.Latency += timestep.Latency;

                ts++;
            }
            Console.WriteLine("Done analyzing");

            var reportPath = "res/report.json";
            Console.WriteLine($"Writing report to: {reportPath}");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(simReport, new JsonSerializerOptions { }));
        }
    }
}