namespace LeoQ.Core.Models;

public sealed record RunResult(
    string ModelName,
    string ScenarioName,
    int Seed,
    double DistanceKm,
    int HopCount,
    double LatencyMs
);
