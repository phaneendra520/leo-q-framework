using LeoQ.Core.Abstractions;
using LeoQ.Core.Models;

namespace LeoQ.Sim.Models;

public sealed class FiberBaselineModel : ILatencyModel
{
    public string Name => "Baseline.Fiber";

    public RunResult Run(ScenarioConfig s)
    {
        double propagationMs = s.DistanceKm / s.FiberSpeedKmPerMs;
        double hopPenaltyMs = 0.15 * s.HopCount;
        double latency = propagationMs + hopPenaltyMs;

        return new RunResult(Name, s.ScenarioName, s.Seed, s.DistanceKm, s.HopCount, latency);
    }
}
