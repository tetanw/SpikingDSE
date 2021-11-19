using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SpikingDSE
{
    public class Layer
    {
        public int InputSize { get; protected set; }
        public int Size { get; protected set; }
        public string Name { get; protected set; }
    }

    public class InputLayer : Layer
    {
        public readonly ISpikeSource spikeSource;

        public InputLayer(ISpikeSource spikeSource, string name = null)
        {
            this.spikeSource = spikeSource;
            this.Name = name;
            this.InputSize = -1;
            this.Size = spikeSource.NrNeurons();
        }
    }

    public enum ResetMode
    {
        Zero,
        Subtract
    }

    public class LIFLayer : Layer
    {
        public float[] pots;
        public float[,] weights;
        public float threshold;
        public float leakage;
        public bool[] spiked;
        private bool refractory;
        public ResetMode resetMode;

        public LIFLayer(float[,] weights, float threshold = 30, float leakage = 0, bool refractory = true, ResetMode resetMode = ResetMode.Zero, string name = "")
        {
            this.InputSize = weights.GetLength(0);
            this.Size = weights.GetLength(1);
            this.weights = weights;
            this.pots = new float[Size];
            this.spiked = new bool[Size];
            this.threshold = threshold;
            this.leakage = leakage;
            this.refractory = refractory;
            this.resetMode = resetMode;
            this.Name = name;
        }

        public void Leak()
        {
            for (int dst = 0; dst < Size; dst++)
            {
                if (spiked[dst])
                {
                    spiked[dst] = false;
                }
                pots[dst] = pots[dst] * leakage;
            }
        }

        public void Integrate(int neuron)
        {
            for (int dst = 0; dst < Size; dst++)
            {
                pots[dst] += weights[neuron, dst];
            }
        }

        public IEnumerable<int> Threshold()
        {
            for (int dst = 0; dst < Size; dst++)
            {
                if (spiked[dst] && refractory)
                    continue;

                if (pots[dst] >= threshold)
                {
                    if (resetMode == ResetMode.Zero)
                        pots[dst] = 0;
                    else if (resetMode == ResetMode.Subtract)
                        pots[dst] -= threshold;
                    else
                        throw new Exception("Unknown reset behaviour");
                    spiked[dst] = true;
                    yield return dst;
                }
            }
        }
    }

    public class RLIFLayer : Layer
    {
        public readonly float[,] InWeights;
        public readonly float[,] RecWeights;
        public float[] Pots;
        public float Leakage;
        public bool[,] Spiked;
        public ResetMode ResetMode;
        public float Thr;

        public RLIFLayer(float[,] inWeights, float[,] recWeights, string name)
        {
            this.InputSize = inWeights.GetLength(0) + recWeights.GetLength(0);
            this.Size = inWeights.GetLength(1);
            this.Pots = new float[Size];
            this.InWeights = inWeights;
            this.RecWeights = recWeights;
            this.Name = name;
            this.InputSize = inWeights.GetLength(0);
            this.Size = inWeights.GetLength(1);
        }

        public void Leak()
        {
            for (int dst = 0; dst < Size; dst++)
            {
                Pots[dst] = Pots[dst] * Leakage;
            }
        }

        public void IntegrateForward(int neuron)
        {
            for (int dst = 0; dst < Size; dst++)
            {
                Pots[dst] += InWeights[neuron, dst];
            }
        }

        public void IntegrateFeedback(int neuron)
        {
            for (int dst = 0; dst < Size; dst++)
            {
                Pots[dst] += RecWeights[neuron, dst];
            }
        }

        public IEnumerable<int> Threshold()
        {
            for (int dst = 0; dst < Size; dst++)
            {
                if (Pots[dst] >= Thr)
                {
                    if (ResetMode == ResetMode.Zero)
                        Pots[dst] = 0;
                    else if (ResetMode == ResetMode.Subtract)
                        Pots[dst] -= Thr;
                    else
                        throw new Exception("Unknown reset behaviour");
                    yield return dst;
                }
            }
        }
    }

    public class WeigthsUtil
    {
        public static float[,] Normalize(float[,] pre, float scale = 1.0f, float bias = 0.0f)
        {
            int width = pre.GetLength(0);
            int height = pre.GetLength(1);

            float[,] post = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    post[x, y] = pre[x, y] * scale + bias;
                }
            }

            return post;
        }

        public static double[,] ReadFromCSVDouble(string path, bool headers = false, bool applyCorrection = false)
        {
            return ReadFromCSV(path, double.Parse, headers, applyCorrection);
        }

        public static float[,] ReadFromCSVFloat(string path, bool headers = false, bool applyCorrection = false)
        {
            return ReadFromCSV(path, float.Parse, headers, applyCorrection);
        }

        public static int[,] ReadFromCSVInt(string path, bool headers = false, bool applyCorrection = false)
        {
            return ReadFromCSV(path, int.Parse, headers, applyCorrection);
        }

        private static T[,] ReadFromCSV<T>(string path, Func<string, T> conv, bool headers = false, bool applyCorrection = false)
        {
            T[,] weights = null;
            int currentLine = 0;
            var lines = File.ReadAllLines(path).Skip(headers ? 1 : 0).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                T[] numbers = line.Split(",").Skip(headers ? 1 : 0).Select(conv).ToArray();
                if (weights == null)
                {
                    int nrSrc = numbers.Length;
                    int nrDest = lines.Length;
                    weights = new T[nrSrc, nrDest];
                }

                for (int j = 0; j < numbers.Length; j++)
                {
                    weights[j, currentLine] = numbers[j];
                }
                currentLine++;
            }

            if (applyCorrection)
            {
                CorrectWeights(weights);
            }

            return weights;
        }

        private static void Swap<T>(int c, int x, int y, T[,] array)
        {
            // swap index x and y
            var buffer = array[c, x];
            array[c, x] = array[c, y];
            array[c, y] = buffer;
        }

        private static void CorrectWeights<T>(T[,] weights)
        {
            int width = weights.GetLength(0);
            int height = weights.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y += 8)
                {
                    Swap(x, y + 0, y + 7, weights);
                    Swap(x, y + 1, y + 6, weights);
                    Swap(x, y + 2, y + 5, weights);
                    Swap(x, y + 3, y + 4, weights);
                }
            }
        }

        public static void ToCSV<T>(string path, T[,] weights)
        {
            StreamWriter sw = new StreamWriter(path);
            int width = weights.GetLength(0);
            int height = weights.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                string[] parts = new string[width];
                for (int x = 0; x < width; x++)
                {
                    parts[x] = weights[x, y].ToString();
                }
                sw.WriteLine(string.Join(",", parts));
            }


            sw.Flush();
            sw.Close();
        }
    }

    public struct NeuronRange
    {
        public readonly int Start;
        public readonly int End;

        public NeuronRange(int start, int end)
        {
            this.Start = start;
            this.End = end;
        }

        public bool Contains(int number)
        {
            return number >= Start && number < End;
        }

        public override string ToString()
        {
            return $"[{Start}, {End})";
        }
    }

    public class SNN
    {
        public List<Layer> layers = new List<Layer>();

        public void AddLayer(Layer layer)
        {
            layers.Add(layer);
        }

        public List<Layer> GetAllLayers()
        {
            return layers;
        }

        public int FindIndex(Layer layer)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if (l == layer)
                {
                    return i;
                }
            }

            return -1;
        }

        public Layer GetSourceLayer(Layer layer)
        {
            int index = FindIndex(layer);
            if (index == -1) return null;
            return index == 0 ? null : layers[index - 1];
        }

        public Layer GetDestLayer(Layer layer)
        {
            int index = FindIndex(layer);
            if (index == -1) return null;
            return index == layers.Count - 1 ? null : layers[index + 1];
        }

        public bool IsInputLayer(Layer layer)
        {
            return FindIndex(layer) == 0;
        }

        public bool IsOutputLayer(Layer layer)
        {
            return FindIndex(layer) == layers.Count - 1;
        }
    }
}