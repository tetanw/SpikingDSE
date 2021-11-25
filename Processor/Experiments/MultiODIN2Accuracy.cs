using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpikingDSE;

class ExpRun
{
    public delegate void TimestepFinished(int ts, HiddenLayer layer);

    public TimestepFinished OnTimestepFinished;

    private Simulator sim;
    private MeshRouter[,] routers;
    private SNN snn;
    private LIFLayer outputLayer;

    private ISpikeSource spikeSource;
    private int interval;
    private int feedbackSize;

    public Stopwatch SimTime;
    public int Predicted;

    public ExpRun(ISpikeSource spikeSource, int interval, int feedbackSize)
    {
        this.spikeSource = spikeSource;
        this.interval = interval;
        this.feedbackSize = feedbackSize;
    }

    private ODINController2 AddController(SNN snn, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ODINController2(controllerCoord, 100, snn, 0, interval, name: "controller"));
        sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
        return controller;
    }

    private ODINCore2 AddCore(ODINDelayModel delayModel, int size, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new ODINCore2(coreCoord, size, delayModel, feedbackBufferSize: feedbackSize, name: name));
        core.OnTimeReceived += (_, _, ts, layer) => OnTimestepFinished?.Invoke(ts, layer);
        sim.AddChannel(core.output, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, core.input);
        return core;
    }

    private void Setup(float[,] weigths1, float[,] weights2, float[,] weights3, float[,] weights4, float[,] weights5)
    {

        // SNN
        float alpha1 = (float)Math.Exp(-1.0 * 1.0 / 10.0);
        snn = new SNN();
        var input = new InputLayer(spikeSource, name: "input");
        snn.AddLayer(input);
        var hidden1 = new RLIFLayer(
            weigths1,
            weights2,
            name: "hidden1"
        );
        hidden1.Leakage = alpha1;
        hidden1.Thr = 0.01f;
        hidden1.ResetMode = ResetMode.Subtract;
        snn.AddLayer(hidden1);

        var hidden2 = new RLIFLayer(
            weights3,
            weights4,
            name: "hidden2"
        );
        hidden2.Leakage = alpha1;
        hidden2.Thr = 0.01f;
        hidden2.ResetMode = ResetMode.Subtract;
        snn.AddLayer(hidden2);

        float alpha2 = (float)Math.Exp(-1.0 * 1.0 / 15.0);
        outputLayer = new LIFLayer(
            weights5,
            name: "output"
        );
        outputLayer.leakage = alpha2;
        outputLayer.Thr = 0.00f;
        outputLayer.ResetMode = ResetMode.Subtract;
        snn.AddLayer(outputLayer);

        // Hardware
        int width = 3;
        int height = 2;
        var delayModel = new ODINDelayModel
        {
            InputTime = 7,
            ComputeTime = 2,
            OutputTime = 8,
            TimeRefTime = 2
        };

        routers = MeshUtils.CreateMesh(sim, width, height, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));

        var controller = AddController(snn, 0, 0);
        var core1 = AddCore(delayModel, 1024, 0, 1, "core1");
        var core2 = AddCore(delayModel, 1024, 1, 1, "core2");
        var core3 = AddCore(delayModel, 1024, 2, 1, "core3");

        // Do mapping
        var mapper = new FirstFitMapper(snn, new Core[] { controller, core1, core2, core3 });
        var mapping = new Mapping();
        mapper.OnMappingFound += mapping.Map;
        mapper.Run();

        foreach (var (layer, core) in mapping._forward)
        {
            if (core is not ODINCore2) continue;
            controller.LayerToCoord(layer, (MeshCoord)core.GetLocation());
        }

        foreach (var core in mapping.Cores)
        {
            if (core is not ODINCore2) continue;

            var destLayer = snn.GetDestLayer(mapping.Reverse[core]);
            MeshCoord dest;
            if (destLayer == null)
                dest = (MeshCoord)controller.GetLocation();
            else
                dest = (MeshCoord)mapping.Forward[destLayer].GetLocation();

            ((ODINCore2)core).setDestination(dest);
        }
    }

    static float[] Softmax(float[] vector)
    {
        float[] res = new float[vector.Length];
        float sum = 0.0f;
        for (int i = 0; i < vector.Length; i++)
        {
            res[i] = (float)Math.Exp(vector[i]);
            sum += res[i];
        }
        for (int i = 0; i < vector.Length; i++)
        {
            res[i] = res[i] / sum;
        }
        return res;
    }

    public void Run(float[,] weigths1, float[,] weights2, float[,] weights3, float[,] weights4, float[,] weights5)
    {
        float[] output = new float[20];
        this.OnTimestepFinished = (ts, layer) =>
        {
            if (layer != outputLayer) return;

            if (ts > 0)
            {
                float[] softmax = Softmax(outputLayer.Pots);
                for (int i = 0; i < 20; i++)
                {
                    output[i] += softmax[i];
                }
            }
        };
        sim = new Simulator();
        Setup(weigths1, weights2, weights3, weights4, weights5);
        sim.Compile();
        SimTime = new Stopwatch();
        SimTime.Start();
        var (time, _) = sim.RunUntil();
        SimTime.Stop();

        Predicted = output.ToList().IndexOf(output.Max());
    }
}

public class MultiOdin2Accuracy
{
    private float[,] weights1;
    private float[,] weights2;
    private float[,] weights3;
    private float[,] weights4;
    private float[,] weights5;

    public void Run()
    {
        string folderPath = "res/multi-odin/validation/normal";

        float alpha1 = (float)Math.Exp(-1.0 * 1.0 / 10.0);
        float beta1 = 1 - alpha1;
        float alpha2 = (float)Math.Exp(-1.0 * 1.0 / 15.0);
        float beta2 = 1 - alpha2;
        weights1 = WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_i_2_h1_n.csv", headers: true), scale: beta1);
        weights2 = WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h1_2_h1_n.csv", headers: true), scale: beta1);
        weights3 = WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h1_2_h2_n.csv", headers: true), scale: beta1);
        weights4 = WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h2_2_h2_n.csv", headers: true), scale: beta1);
        weights5 = WeigthsUtil.Normalize(WeigthsUtil.ReadFromCSVFloat($"{folderPath}/weights_h2o_n.csv", headers: true), scale: beta2);

        int[] confs = new int[] {
            1000, 2000, 5000, 7500,
            10_000, 25_000, 50_000, 75_000, 100_000, 150_000, 200_000,
            250_000, 300_000, 350_000, 400_000, 450_000, 500_000,
            550_000, 600_000, 650_000, 700_000, 750_000, 800_000,
            850_000, 900_000, 950_000, 1_000_000
        };
        int nrInputs = 512;
        for (int j = 0; j < confs.Length; j++)
        {
            int nrCorrect = 0;
            for (int i = 0; i < nrInputs; i++)
            {
                bool correct = RunInput(new InputTraceFile($"res/multi-odin/inputs/input_{i}.trace", 700), confs[j], 128, i);
                if (correct) nrCorrect++;
            }
            float accuracy = (float)nrCorrect / nrInputs * 100;
            Console.WriteLine($"{confs[j]};{accuracy}");
        }

        // int[] confs = new int[] {
        //     // 0, 1, 2, 4, 8, 16, 32
        //     64, 128
        // };
        // int nrInputs = 512;
        // for (int j = 0; j < confs.Length; j++)
        // {
        //     int nrCorrect = 0;
        //     for (int i = 0; i < nrInputs; i++)
        //     {
        //         bool correct = RunInput(new InputTraceFile($"res/multi-odin/inputs/input_{i}.trace", 700), 1_000_000, confs[j], i);
        //         if (correct) nrCorrect++;
        //     }
        //     float accuracy = (float)nrCorrect / nrInputs * 100;
        //     Console.WriteLine($"{confs[j]};{accuracy}");
        // }

        // int nrInputs = 512;
        // int nrCorrect = 0;
        // for (int i = 0; i < nrInputs; i++)
        // {
        //     bool correct = RunInput(new InputTraceFile($"res/multi-odin/inputs/input_{i}.trace", 700), 50_000, 64, i);
        //     if (correct) nrCorrect++;
        // }
        // float accuracy = (float)nrCorrect / nrInputs * 100;
        // Console.WriteLine($"Accuracy: {accuracy}");
    }

    bool RunInput(InputTraceFile traceFile, int interval, int feedbackSize, int inputNr)
    {
        var run = new ExpRun(traceFile, interval, feedbackSize);
        run.Run(weights1, weights2, weights3, weights4, weights5);
        // Console.WriteLine($"Running input: {inputNr}");
        // Console.WriteLine($"Time taken: {run.SimTime.ElapsedMilliseconds} ms");
        // string match = run.Predicted == traceFile.Correct ? "YES" : "NO";
        // Console.WriteLine($"[{inputNr}]: Predicted: {run.Predicted}, Correct: {traceFile.Correct}, Match: {match}");
        return run.Predicted == traceFile.Correct;
    }
}