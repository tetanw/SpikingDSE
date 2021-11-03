using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SpikingDSE
{
    public abstract class Layer
    {
        public int Size { get; protected set; }
    }

    public class InputLayer : Layer
    {
        private string name;
        public readonly IEnumerable<int> inputSpikes;

        public InputLayer(int size, IEnumerable<int> inputSpikes, string name = null)
        {
            this.inputSpikes = inputSpikes;
            this.name = name;
            this.Size = size;
        }
    }

    public abstract class HiddenLayer : Layer
    {
        public abstract void SetNeuronRange(NeuronRange range);
        public abstract void SetInputRange(NeuronRange range);
    }

    public class ODINLayer : HiddenLayer
    {
        public int[] pots;
        public int[,] weights;
        public int threshold;

        public NeuronRange NeuronRange;
        public NeuronRange InputRange;

        public ODINLayer(int[,] weights, int threshold = 30, string name = "")
        {
            int from = weights.GetLength(0);
            int to = weights.GetLength(1);
            this.weights = weights;
            pots = new int[to];
            this.Size = to;
            this.Name = name;
            this.threshold = threshold;
        }

        public string Name { get; }

        public override void SetInputRange(NeuronRange range)
        {
            this.InputRange = range;
        }

        public override void SetNeuronRange(NeuronRange range)
        {
            this.NeuronRange = range;
        }
    }

    public class WeigthsUtil
    {
        public static int[,] ReadFromCSV(string path)
        {
            int[,] weights = null;
            int currentLine = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                int[] numbers = line.Split(",").Select(t => int.Parse(t)).ToArray();
                if (weights == null)
                {
                    weights = new int[numbers.Length, numbers.Length];
                }

                for (int i = 0; i < numbers.Length; i++)
                {
                    weights[i, currentLine] = numbers[i];
                }
                currentLine++;
            }

            CorrectWeights(weights);
            return weights;
        }
        private static void Swap(int c, int x, int y, int[,] array)
        {
            // swap index x and y
            var buffer = array[c, x];
            array[c, x] = array[c, y];
            array[c, y] = buffer;
        }

        private static void CorrectWeights(int[,] weights)
        {
            for (int x = 0; x < 256; x++)
            {
                for (int y = 0; y < 256; y += 8)
                {
                    Swap(x, y + 0, y + 7, weights);
                    Swap(x, y + 1, y + 6, weights);
                    Swap(x, y + 2, y + 5, weights);
                    Swap(x, y + 3, y + 4, weights);
                }
            }
        }

        public static void ToCSV(string path, int[,] weights)
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

   
}