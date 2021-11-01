using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpikingDSE
{    
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