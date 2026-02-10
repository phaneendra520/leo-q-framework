using System;
using System.Collections.Generic;
using System.Text;

namespace LeoQ.Core.Stats
{

    public static class LatencyStats
    {
        public static double Percentile(IReadOnlyList<double> values, double percentile)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("Values must not be empty");

            var sorted = values.OrderBy(v => v).ToArray();

            double rank = (percentile / 100.0) * (sorted.Length - 1);
            int low = (int)Math.Floor(rank);
            int high = (int)Math.Ceiling(rank);

            if (low == high)
                return sorted[low];

            double weight = rank - low;
            return sorted[low] * (1 - weight) + sorted[high] * weight;
        }
    }

}
