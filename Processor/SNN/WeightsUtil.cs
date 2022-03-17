using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SpikingDSE;

public static class WeigthsUtil
{
    public static R[,] Transform<T, R>(this T[,] items, Func<int, int, T, R> f)
    {
        int d0 = items.GetLength(0);
        int d1 = items.GetLength(1);
        R[,] result = new R[d0, d1];
        for (int i0 = 0; i0 < d0; i0 += 1)
            for (int i1 = 0; i1 < d1; i1 += 1)
                result[i0, i1] = f(i0, i1, items[i0, i1]);
        return result;
    }

    public static R[] Transform<T, R>(this T[] items, Func<int, T, R> f)
    {
        int d0 = items.GetLength(0);
        R[] result = new R[d0];
        for (int i0 = 0; i0 < d0; i0 += 1)
            result[i0] = f(i0, items[i0]);
        return result;
    }

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

    public static double[,] Read2DDouble(string path, bool headers = false, bool applyCorrection = false)
    {
        return Read2D(path, double.Parse, headers, applyCorrection);
    }

    public static float[,] Read2DFloat(string path, bool headers = false, bool applyCorrection = false)
    {
        var array2d = Read2D(path, float.Parse, headers, applyCorrection);
        return array2d;
    }

    public static float[] Read1DFloat(string path, bool headers = false)
    {
        var array2D = Read2DFloat(path, headers);
        var array1D = Flatten(array2D);
        return array1D;
    }

    private static float[] Flatten(float[,] input)
    {
        int size = input.GetLength(1);
        float[] res = new float[size];
        for (int i = 0; i < size; i++)
        {
            res[i] = input[0, i];
        }
        return res;
    }

    public static int[,] ReadFromCSVInt(string path, bool headers = false, bool applyCorrection = false)
    {
        return Read2D(path, int.Parse, headers, applyCorrection);
    }

    private static T[,] Read2D<T>(string path, Func<string, T> conv, bool headers = false, bool applyCorrection = false)
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
        (array[c, y], array[c, x]) = (array[c, x], array[c, y]);
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
        var sw = new StreamWriter(path);
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

    public static float Exp(int _, float value)
    {
        return (float)Math.Exp(-1.0f / value);
    }

    public static Func<int, int, float, float> ScaleWeights(float[] beta)
    {
        return (x, y, f) => f * beta[y];
    }

    public static float[,] Slice(float[,] parent, int sX, int sY, int sWidth, int sHeight)
    {
        // int width = parent.GetLength(0);
        // int height = parent.GetLength(1);
        var slice = new float[sHeight, sWidth];
        for (int yy = 0; yy < sHeight; yy++)
        {
            for (int xx = 0; xx < sWidth; xx++)
            {
                slice[yy, xx] = parent[yy + sY, xx + sX];
            }
        }
        return slice;
    }

    public static float[] Slice(float[] parent, int sOffset, int sLength)
    {
        // int length = parent.GetLength(0);
        var slice = new float[sLength];
        for (int i = 0; i < sLength; i++)
        {
            slice[i] = parent[i + sOffset];
        }
        return slice;
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
