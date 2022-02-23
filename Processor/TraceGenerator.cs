using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SpikingDSE;

public class TraceGenerator
{
    public void Run()
    {
        int nrLayers = 2;
        var srnnTemplate = SRNN.Load("res/snn/best", 700, nrLayers);
        var spikeCounts = new Dictionary<(string, int), int>();
        var countsFile = new StreamWriter("res/multi-core/counts.csv");
        countsFile.WriteLine("layer,ts,count");

        var runningTime = new Stopwatch();
        runningTime.Start();
        for (int i = 0; i < 2264; i++)
        {
            var inputFile = new InputTraceFile($"res/shd/input_{i}.trace", 700, 100);
            var srnn = srnnTemplate.Copy();

            int ts = 0;
            while (inputFile.NextTimestep())
            {
                if (!spikeCounts.ContainsKey(("i", ts))) spikeCounts[("i", ts)] = 0;
                for (int l = 1; l <= nrLayers; l++)
                {
                    if (!spikeCounts.ContainsKey(($"h{l}", ts))) spikeCounts[($"h{l}", ts)] = 0;
                }

                // Feed first layer
                foreach (var spike in inputFile.NeuronSpikes())
                {
                    spikeCounts[(srnn.Input.Name, ts)]++;
                    srnn.Hidden[0].Forward(spike);
                }

                for (int l = 0; l < nrLayers - 1; l++)
                {
                    foreach (var spike in srnn.Hidden[l].Sync())
                    {
                        spikeCounts[(srnn.Hidden[l].Name, ts)]++;
                        srnn.Hidden[l + 1].Forward(spike);
                    }
                }

                foreach (var spike in srnn.Hidden[nrLayers - 1].Sync())
                {
                    spikeCounts[(srnn.Hidden[nrLayers - 1].Name, ts)]++;
                    srnn.Output.Forward(spike);
                }

                ts++;
            }
        }
        runningTime.Stop();
        Console.WriteLine($"Elapsed time: {runningTime.ElapsedMilliseconds:n} ms");
        foreach (var ((layer, ts), count) in spikeCounts)
        {
            countsFile.WriteLine($"{layer},{ts},{count}");
        }
        countsFile.Close();
    }
}