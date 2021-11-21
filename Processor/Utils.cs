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

    public class Map<T1, T2>
    {
        // TODO: Fix public -> private
        public Dictionary<T1, T2> _forward = new Dictionary<T1, T2>();
        private Dictionary<T2, T1> _reverse = new Dictionary<T2, T1>();

        public Map()
        {
            this.Forward = new Indexer<T1, T2>(_forward);
            this.Reverse = new Indexer<T2, T1>(_reverse);
        }

        public class Indexer<T3, T4>
        {
            private Dictionary<T3, T4> _dictionary;
            public Indexer(Dictionary<T3, T4> dictionary)
            {
                _dictionary = dictionary;
            }
            public T4 this[T3 index]
            {
                get { return _dictionary[index]; }
                set { _dictionary[index] = value; }
            }
        }

        public void Add(T1 t1, T2 t2)
        {
            _forward.Add(t1, t2);
            _reverse.Add(t2, t1);
        }

        public IEnumerable<T1> Forwards() => _forward.Keys;
        public IEnumerable<T2> Backwards() => _forward.Values; 

        public Indexer<T1, T2> Forward { get; private set; }
        public Indexer<T2, T1> Reverse { get; private set; }
    }
}