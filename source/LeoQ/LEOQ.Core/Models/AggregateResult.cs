using System;
using System.Collections.Generic;
using System.Text;

namespace LeoQ.Core.Models
{
    public sealed record AggregateResult(
        string ModelName,
        string ScenarioName,
        int Runs,
        double DistanceKm,
        int HopCount,
        double P50LatencyMs,
        double P95LatencyMs
    );

}
