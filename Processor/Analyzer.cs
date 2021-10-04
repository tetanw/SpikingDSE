using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace SpikingDSE
{
    public class Analyzer
    {
        private TensorFileGroup input;
        private HWConfig hwConf;
        private SNNConfig snnConf;
        private CostConfig costConf;
        private Mapper mapper;
        private SNN snn;
        private string strategyStr;

        public Analyzer(string snnPath, string hwPath, string costPath, string strategyStr)
        {
            this.hwConf = JsonSerializer.Deserialize<HWConfig>(File.ReadAllText(hwPath));
            this.snnConf = JsonSerializer.Deserialize<SNNConfig>(File.ReadAllText(snnPath));
            this.costConf = JsonSerializer.Deserialize<CostConfig>(File.ReadAllText(costPath));

            // the creation of the mapper will be done later
            this.strategyStr = strategyStr;
        }

        private void InitMapping()
        {
            if (strategyStr.Equals("round-robin"))
            {
                mapper = new RoundRobinMapper(snn, hwConf);
            }
            else if (strategyStr.Equals("first-fit"))
            {
                mapper = new FirstFitMapper(snn, hwConf);
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
            var coreSpikes = new SpikeMap[hwConf.NrPEs];
            for (int i = 0; i < hwConf.NrPEs; i++)
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

        public void Run(int maxTimesteps)
        {
            if (hwConf.PipelineLat < hwConf.PipelineII)
            {
                throw new Exception("Latency must at least be the II");
            }

            InitSNN();
            InitMapping();

            Console.WriteLine("Start analyzing");
            var report = new AnalysisReport();
            report.Analyses.Add(new MappingReport()
            {
                Mapping = mapper.coreTable
            });
            var sim = new SimAnalysis();
            report.Analyses.Add(sim);
            int TS = 0;
            while (!input.NextTimestep() && TS < maxTimesteps)
            {
                var allSpikes = input.NeuronSpikes();
                var allSpikeRoutes = mapper.GetAllSpikeRoutes(allSpikes);
                var coreSpikeMap = GetCoreSpikeMap(allSpikeRoutes);

                var timestep = new TimestepAnalysis(TS);
                report.Analyses.Add(timestep);
                timestep.SpikeRoutes = allSpikeRoutes;
                for (int i = 0; i < hwConf.NrPEs; i++)
                {
                    var coreSpikes = coreSpikeMap[i];
                    var core = new PEAnalysis(i, TS, coreSpikes, hwConf, costConf);
                    report.Analyses.Add(core);
                    timestep.Energy += core.Energy;
                    timestep.Latency += core.Latency;
                    // TODO: Solve this in a nicer way
                    timestep.Latency.Frequency = hwConf.Frequency;
                    timestep.Energy.Time = timestep.Latency.TotalSecs;
                }
                sim.Energy += timestep.Energy;
                sim.Latency += timestep.Latency;

                TS++;
            }
            // TODO: This is not elegant
            sim.Latency.Frequency = hwConf.Frequency;
            sim.Energy.Time = sim.Latency.TotalSecs;
            Console.WriteLine("Done analyzing");

            var reportPath = "res/report.json";
            Console.WriteLine($"Writing report to: {reportPath}");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { }));
        }
    }
}