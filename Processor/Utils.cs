using System;
using System.Collections.Generic;

namespace SpikingDSE
{
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
            switch (rank)
            {
                case 0:
                    return "";
                case -1:
                    return "m";
                case -2:
                    return "µ";
                case -3:
                    return "n";
                case 1:
                    return "K";
                case 2:
                    return "M";
                case 3:
                    return "G";
                default:
                    throw new Exception($"Rank ({rank}) not supported");
            }
        }

        private static double TruncateToSignificantDigits(double d, int digits)
        {
            if (d == 0)
                return 0;

            double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1 - digits);
            return scale * Math.Truncate(d / scale);
        }

        public static string GetPrefix(double number, int digits = 3)
        {
            var (rank, b) = GetRank(number);
            string prefix = GetPrefix(rank);

            // TODO: Will make numbers like 0.000 instead of 0.0
            return $"{b:#,0.000} {prefix}";
        }

        public static string FormatSI(double number, string unit, int digits = 3)
        {
            return $"{GetPrefix(number, digits)}{unit}";
        }
    }

    public static class ArrayExtensions
    {
        public static T[] Concat<T>(this T[] x, T[] y)
        {
            if (x == null) throw new ArgumentNullException("x");
            if (y == null) throw new ArgumentNullException("y");
            int oldLen = x.Length;
            Array.Resize<T>(ref x, x.Length + y.Length);
            Array.Copy(y, 0, x, oldLen, y.Length);
            return x;
        }

        public static bool Any<T>(this T[] array, Predicate<T> when)
        {
            if (array == null) throw new ArgumentNullException("array");
            foreach (var item in array)
            {
                if (when(item))
                    return true;
            }
            return false;
        }
    }
}