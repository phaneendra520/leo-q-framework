using LeoQ.Core.Abstractions;
using LeoQ.Core.Models;
using LeoQ.Core.Random;

namespace LeoQ.Sim.Models;

public sealed class InternetBaselineModel : ILatencyModel
{
    public string Name => "Baseline.Internet";

    public RunResult Run(ScenarioConfig s)
    {
        var dr = new DeterministicRandom(s.Seed);

        double propagationMs = s.DistanceKm / s.FiberSpeedKmPerMs;
        double overheadMs = s.BaseInternetOverheadMs + (0.6 * s.HopCount);

        double jitterMs = dr.NextGaussian(0, s.JitterStdDevMs);
        jitterMs = System.Math.Max(-3.0 * s.JitterStdDevMs,
                                  System.Math.Min(3.0 * s.JitterStdDevMs, jitterMs));

        double latency = propagationMs + overheadMs + jitterMs;

        return new RunResult(Name, s.ScenarioName, s.Seed, s.DistanceKm, s.HopCount, latency);
    }
}
