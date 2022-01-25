// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.IO;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;

// namespace SpikingDSE;

// class ModelWeights3
// {
//     // Hidden 1
//     public float[,] weights_i_2_h1;
//     public float[,] weights_h1_2_h1;
//     public float[] bias_h1;
//     public float[] alpha_h1;
//     public float[] rho_h1;

//     // Hidden 2
//     public float[,] weights_h1_2_h2;
//     public float[,] weights_h2_2_h2;
//     public float[] bias_h2;
//     public float[] alpha_h2;
//     public float[] rho_h2;

//     // Output
//     public float[,] weights_h2_o;
//     public float[] alpha_o;
// }

// class ExpRun3
// {
//     public delegate void TimestepFinished(int ts, HiddenLayer layer);

//     public TimestepFinished OnTimestepFinished;

//     private Simulator sim;
//     private MeshRouter[,] routers;
//     private SNN snn;
//     private IFLayer outputLayer;

//     private ISpikeSource spikeSource;
//     private int interval;
//     private int feedbackSize;

//     public Stopwatch SimTime;
//     public int Predicted;

//     public ModelWeights3 Weights;

//     public ExpRun3(ISpikeSource spikeSource, ModelWeights3 weights, int interval, int feedbackSize)
//     {
//         this.spikeSource = spikeSource;
//         this.interval = interval;
//         this.feedbackSize = feedbackSize;
//         this.Weights = weights;
//     }

//     private ProtoController AddController(SNN snn, int x, int y)
//     {
//         var controllerCoord = new MeshCoord(x, y);
//         var controller = sim.AddActor(new ProtoController(controllerCoord, 100, snn, 0, interval, name: "controller"));
//         sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
//         sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
//         return controller;
//     }

//     private ProtoCore AddCore(ODINDelayModel delayModel, int size, int x, int y, string name)
//     {
//         var coreCoord = new MeshCoord(x, y);
//         var core = sim.AddActor(new ProtoCore(coreCoord, size, delayModel, feedbackBufferSize: feedbackSize, name: name));
//         core.OnSyncEnded += (_, _, ts, layer) =>
//         {
//             OnTimestepFinished?.Invoke(ts, layer);
//         };
//         sim.AddChannel(core.output, routers[x, y].inLocal);
//         sim.AddChannel(routers[x, y].outLocal, core.input);
//         return core;
//     }

//     private void Setup()
//     {

//         // SNN
//         float alpha1 = (float)Math.Exp(-1.0 * 1.0 / 10.0);
//         snn = new SNN();
//         var input = new InputLayer(spikeSource, name: "input");
//         snn.AddLayer(input);
//         var hidden1 = new ALIFLayer(
//             Weights.weights_i_2_h1,
//             Weights.weights_h1_2_h1,
//             Weights.bias_h1,
//             Weights.alpha_h1,
//             Weights.rho_h1,
//             0.01f,
//             name: "hidden1"
//         );
//         snn.AddLayer(hidden1);

//         var hidden2 = new ALIFLayer(
//             Weights.weights_h1_2_h2,
//             Weights.weights_h2_2_h2,
//             Weights.bias_h2,
//             Weights.alpha_h2,
//             Weights.rho_h2,
//             0.01f,
//             name: "hidden2"
//         );
//         snn.AddLayer(hidden2);

//         float alpha2 = (float)Math.Exp(-1.0 * 1.0 / 15.0);
//         outputLayer = new IFLayer(
//             Weights.weights_h2_o,
//             Weights.alpha_o,
//             name: "output"
//         );
//         snn.AddLayer(outputLayer);

//         // Hardware
//         int width = 3;
//         int height = 2;
//         var delayModel = new ODINDelayModel
//         {
//             InputTime = 7,
//             ComputeTime = 2,
//             OutputTime = 8,
//             TimeRefTime = 2
//         };

//         routers = MeshUtils.CreateMesh(sim, width, height, (x, y) => new ProtoXYRouter(x, y, name: $"router({x},{y})"));

//         var controller = AddController(snn, 0, 0);
//         var core1 = AddCore(delayModel, 1024, 0, 1, "core1");
//         var core2 = AddCore(delayModel, 1024, 1, 1, "core2");
//         var core3 = AddCore(delayModel, 1024, 2, 1, "core3");

//         // Do mapping
//         var mapper = new FirstFitMapper(snn, new Core[] { controller, core1, core2, core3 });
//         var mapping = new Mapping();
//         mapper.OnMappingFound += mapping.Map;
//         mapper.Run();

//         foreach (var (layer, core) in mapping._forward)
//         {
//             if (core is not ProtoCore) continue;
//             controller.LayerToCoord(layer, (MeshCoord)core.GetLocation());
//         }

//         foreach (var core in mapping.Cores)
//         {
//             if (core is not ProtoCore) continue;

//             var destLayer = snn.GetDestLayer(mapping.Reverse[core]);
//             MeshCoord dest;
//             if (destLayer == null)
//                 dest = (MeshCoord)controller.GetLocation();
//             else
//                 dest = (MeshCoord)mapping.Forward[destLayer].GetLocation();

//             ((ProtoCore)core).setDestination(dest);
//         }
//     }

//     static float[] Softmax(float[] vector)
//     {
//         float[] res = new float[vector.Length];
//         float sum = 0.0f;
//         for (int i = 0; i < vector.Length; i++)
//         {
//             res[i] = (float)Math.Exp(vector[i]);
//             sum += res[i];
//         }
//         for (int i = 0; i < vector.Length; i++)
//         {
//             res[i] = res[i] / sum;
//         }
//         return res;
//     }

//     public void Run()
//     {
//         float[] output = new float[20];
//         this.OnTimestepFinished = (ts, layer) =>
//         {
//             if (layer != outputLayer) return;

//             if (ts > 0)
//             {
//                 float[] softmax = Softmax(outputLayer.Readout);
//                 for (int i = 0; i < 20; i++)
//                 {
//                     output[i] += softmax[i];
//                 }
//             }
//         };
//         sim = new Simulator();
//         Setup();
//         sim.Compile();
//         SimTime = new Stopwatch();
//         SimTime.Start();
//         var (time, _) = sim.RunUntil();
//         SimTime.Stop();

//         Predicted = output.ToList().IndexOf(output.Max());
//     }
// }

// public class ProtoMultiCoreAccuracy
// {
//     private ModelWeights3 Weights;

//     private float Exp(int index, float value)
//     {
//         return (float)Math.Exp(-1.0f / value);
//     }

//     private Func<int, int, float, float> ScaleWeights(float[] beta)
//     {
//         return (x, y, f) => f * beta[y];
//     }

//     public void Run()
//     {
//         string folderPath = "res/multi-odin/validation/test6";

//         // Layer 1
//         float[] tau_m1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_h1.csv", headers: true);
//         float[] tau_adp1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_adp_h1.csv", headers: true);
//         float[] alpha1 = tau_m1.Transform(Exp);
//         float[] rho1 = tau_adp1.Transform(Exp);
//         float[] alphaComp1 = alpha1.Transform((_, a) => 1 - a);

//         // Layer 2
//         float[] tau_m2 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_h2.csv", headers: true);
//         float[] tau_adp2 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_adp_h2.csv", headers: true);
//         float[] alpha2 = tau_m2.Transform(Exp);
//         float[] rho2 = tau_adp2.Transform(Exp);
//         float[] alphaComp2 = alpha2.Transform((_, a) => 1 - a);

//         // Output layer
//         float[] tau_m3 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_o.csv", headers: true);
//         float[] alpha3 = tau_m3.Transform(Exp);
//         float[] alphaComp3 = alpha3.Transform((_, a) => 1 - a);

//         Weights = new ModelWeights3()
//         {
//             weights_i_2_h1 = WeigthsUtil.Read2DFloat($"{folderPath}/weights_i_2_h1.csv", headers: true).Transform(ScaleWeights(alphaComp1)),
//             weights_h1_2_h1 = WeigthsUtil.Read2DFloat($"{folderPath}/weights_h1_2_h1.csv", headers: true).Transform(ScaleWeights(alphaComp1)),
//             alpha_h1 = alpha1,
//             rho_h1 = rho1,
//             bias_h1 = WeigthsUtil.Read1DFloat($"{folderPath}/bias_h1.csv", headers: true),

//             weights_h1_2_h2 = WeigthsUtil.Read2DFloat($"{folderPath}/weights_h1_2_h2.csv", headers: true).Transform(ScaleWeights(alphaComp2)),
//             weights_h2_2_h2 = WeigthsUtil.Read2DFloat($"{folderPath}/weights_h2_2_h2.csv", headers: true).Transform(ScaleWeights(alphaComp2)),
//             alpha_h2 = alpha2,
//             rho_h2 = rho2,
//             bias_h2 = WeigthsUtil.Read1DFloat($"{folderPath}/bias_h2.csv", headers: true),

//             weights_h2_o = WeigthsUtil.Read2DFloat($"{folderPath}/weights_h2_2_o.csv", headers: true).Transform(ScaleWeights(alphaComp3)),
//             alpha_o = alpha3
//         };

//         // int[] confs = new int[] {
//         //     1000, 2000, 5000, 7500,
//         //     10_000, 25_000, 50_000, 75_000, 100_000, 150_000, 200_000,
//         //     250_000, 300_000, 350_000, 400_000, 450_000, 500_000,
//         //     550_000, 600_000, 650_000, 700_000, 750_000, 800_000,
//         //     850_000, 900_000, 950_000, 1_000_000
//         // };
//         // int[] confs = new int[] { 1_500_000, 3_000_000 };
//         // int nrInputs = 2264;
//         // var tasks = new List<Task<(int, float)>>();
//         // for (int j = 0; j < confs.Length; j++)
//         // {
//         //     int conf = confs[j];
//         //     tasks.Add(Task.Run(() =>
//         //     {
//         //         Stopwatch simTime = new Stopwatch();
//         //         simTime.Start();
//         //         int nrCorrect = 0;
//         //         for (int i = 0; i < nrInputs; i++)
//         //         {
//         //             bool correct = RunInput(new InputTraceFile($"res/multi-odin/inputs/input_{i}.trace", 700), conf, int.MaxValue, i);
//         //             if (correct) nrCorrect++;
//         //         }
//         //         simTime.Stop();
//         //         Console.WriteLine($"Sim for conf {conf} elapsed in: {simTime.Elapsed.TotalSeconds}s");
//         //         float accuracy = (float)nrCorrect / nrInputs * 100;

//         //         return (conf, accuracy);
//         //     }));
//         // }
//         // var results = Task.WhenAll(tasks).Result;
//         // foreach (var (conf, accuracy) in results)
//         //     Console.WriteLine($"{conf};{accuracy}");


//         // int[] bufferSizes = Enumerable.Range(1, 64).Where(i => i % 2 == 1).ToArray();
//         // int nrInputs = 2264;
//         // var tasks = new List<Task<(int, float)>>();
//         // for (int j = 0; j < bufferSizes.Length; j++)
//         // {
//         //     int conf = bufferSizes[j];
//         //     tasks.Add(Task.Run(() =>
//         //     {
//         //         Stopwatch simTime = new Stopwatch();
//         //         simTime.Start();
//         //         int nrCorrect = 0;
//         //         for (int i = 0; i < nrInputs; i++)
//         //         {
//         //             bool correct = RunInput(new InputTraceFile($"res/multi-odin/inputs/input_{i}.trace", 700), 1_000_000, conf, i);
//         //             if (correct) nrCorrect++;
//         //         }
//         //         simTime.Stop();
//         //         Console.WriteLine($"Sim for conf {conf} elapsed in: {simTime.Elapsed.TotalSeconds}s");
//         //         float accuracy = (float)nrCorrect / nrInputs * 100;
//         //         Console.WriteLine($"{conf};{accuracy}");
//         //         return (conf, accuracy);
//         //     }));
//         // }
//         // var results = Task.WhenAll(tasks).Result;
//         // foreach (var (conf, accuracy) in results)
//         //     Console.WriteLine($"{conf};{accuracy}");

//         int nrInputs = 2264;
//         int nrCorrect = 0;
//         for (int i = 0; i < nrInputs; i++)
//         {
//             bool correct = RunInput(new InputTraceFile($"res/multi-odin/inputs/input_{i}.trace", 700), 100_000_000, int.MaxValue, i);
//             if (correct) nrCorrect++;
//         }
//         float accuracy = (float)nrCorrect / nrInputs * 100;
//         Console.WriteLine($"Accuracy: {accuracy}");
//     }

//     bool RunInput(InputTraceFile traceFile, int interval, int feedbackSize, int inputNr)
//     {
//         var run = new ExpRun3(traceFile, Weights, interval, feedbackSize);
//         run.Run();
//         return run.Predicted == traceFile.Correct;
//     }
// }