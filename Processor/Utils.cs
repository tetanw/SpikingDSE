using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SpikingDSE;

public static class JSONUtils
{
    public static string[] GetStringArray(this JsonElement el)
    {
        var array = el.EnumerateArray().Select(el => el.GetString()).ToArray();
        return array;
    }
}

public static class DicUtils
{
    public static V? GetOptional<K, V>(this Dictionary<K, V> dict, K key) where V : struct
    {
        return dict.TryGetValue(key, out V value) ? value : null;
    }

    public static V Optional<K, V>(this Dictionary<K, V> dict, K key) where V : class
    {
        return dict.TryGetValue(key, out V value) ? value : null;
    }

    public static void AddCount<K>(this Dictionary<K, int> dict, K key, int amount)
    {
        bool found = dict.ContainsKey(key);
        if (found)
        {
            dict[key] += amount;
        }
        else
        {
            dict[key] = amount;
        }
    }
}

public class Measurements
{
    private static (int rank, double b) GetRank(double number)
    {
        int rank = 0;

        // TODO: Implement more decently, 0 is a hard edge case
        if (number >= 1000.0)
        {
            while (number >= 1000.0 && rank <= 3)
            {
                number /= 1000.0;
                rank++;
            }
        }
        else if (number <= 1.0 && number >= 1e-9)
        {
            while (number <= 1.0 && rank >= -3)
            {
                number *= 1000.0;
                rank--;
            }
        }

        return (rank, number);
    }

    private static string GetPrefix(int rank)
    {
        return rank switch
        {
            0 => "",
            -1 => "m",
            -2 => "µ",
            -3 => "n",
            1 => "K",
            2 => "M",
            3 => "G",
            _ => throw new Exception($"Rank ({rank}) not supported"),
        };
    }

    public static string GetPrefix(double number)
    {
        var (rank, b) = GetRank(number);
        string prefix = GetPrefix(rank);

        // TODO: Will make numbers like 0.000 instead of 0.0
        return $"{b:#,0.000} {prefix}";
    }

    public static string FormatSI(double number, string unit)
    {
        return $"{GetPrefix(number)}{unit}";
    }
}

public static class ArrayExtensions
{
    public static T[] Concat<T>(this T[] x, T[] y)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (y == null) throw new ArgumentNullException(nameof(y));
        int oldLen = x.Length;
        Array.Resize<T>(ref x, x.Length + y.Length);
        Array.Copy(y, 0, x, oldLen, y.Length);
        return x;
    }

    public static bool Any<T>(this T[] array, Predicate<T> when)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        foreach (var item in array)
        {
            if (when(item))
                return true;
        }
        return false;
    }
}