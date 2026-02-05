namespace LeoQ.Core.Models;

public sealed record ScenarioConfig(
    string ScenarioName,
    double DistanceKm,
    int HopCount,
    int Seed,
    double FiberSpeedKmPerMs = 200.0,
    double BaseInternetOverheadMs = 8.0,
    double JitterStdDevMs = 1.5
);
