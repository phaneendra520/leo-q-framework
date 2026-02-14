using LeoQ.Core.Abstractions;
using LeoQ.Core.Models;
using LeoQ.Pqc.Models;

namespace LeoQ.Sim.Models;

public sealed class LeoQPolicyRouterModel : ILatencyModel
{
    public string Name => "LEOQ.PolicyRouter";

    // Tuning weights (keep simple and explicit)
    private const double LambdaHandoverRisk = 3.0; // weight on expected handover penalty
    private const double MuCrypto = 1.0;           // weight on PQC overhead

    private const double LightSpeedKmPerMs = 300.0;

    private readonly ICryptoOverheadModel _crypto;

    public LeoQPolicyRouterModel() : this(new SimplePqcOverheadModel()) { }

    public LeoQPolicyRouterModel(ICryptoOverheadModel crypto) => _crypto = crypto;

    public RunResult Run(ScenarioConfig s)
    {
        // Candidate route A: Fast (lower ISL hops, higher handover risk)
        var fast = s with
        {
            LeoIslHopCount = Math.Max(2, s.LeoIslHopCount - 3),
            HandoverProb = Math.Min(0.15, s.HandoverProb * 2.5)
        };

        var stable = s with
        {
            LeoIslHopCount = s.LeoIslHopCount + 3,
            HandoverProb = Math.Max(0.01, s.HandoverProb * 0.3)
        };


        // Score both candidates
        double fastScore = ScoreCandidate(fast);
        double stableScore = ScoreCandidate(stable);

        // Choose best candidate (policy decision)
        var chosen = (stableScore <= fastScore) ? stable : fast;

        // Now “execute” latency sample using the same LEO path logic + crypto overhead
        double latency = SampleLeoLatencyMs(chosen) + _crypto.SampleCryptoOverheadMs(chosen);

        return new RunResult(Name, chosen.ScenarioName, chosen.Seed, chosen.DistanceKm, chosen.HopCount, latency);
    }

    private double ScoreCandidate(ScenarioConfig c)
    {
        // Use expected latency (no random handover) + expected handover penalty + expected crypto overhead
        double expectedLeo = ExpectedLeoLatencyMs(c);
        double expectedHandover = LambdaHandoverRisk * (c.HandoverProb * c.HandoverPenaltyMs);

        // Expected crypto overhead approximated by weighted average:
        double expectedCrypto = (!c.PqcEnabled) ? 0.0
            : MuCrypto * (c.SessionResumptionProb * c.PqcResumptionMs + (1.0 - c.SessionResumptionProb) * c.PqcHandshakeMs);

        return expectedLeo + expectedHandover + expectedCrypto;
    }

    private double ExpectedLeoLatencyMs(ScenarioConfig c)
    {
        double pathKm = LeoPathGeometry.EstimatePathKm(c.DistanceKm, c.LeoAltitudeKm, c.LeoIslHopCount);
        double propagationMs = pathKm / LightSpeedKmPerMs;
        double groundProcMs = 2 * c.LeoGroundProcessingMs;
        double islOverheadMs = c.LeoIslHopCount * c.LeoIslPerHopOverheadMs;

        // expected value excludes the random event; it is accounted in score via expected penalty
        return propagationMs + groundProcMs + islOverheadMs;
    }

    private double SampleLeoLatencyMs(ScenarioConfig c)
    {
        // reuse the baseline LEO model by computing latency with a sampled handover event
        var leoBaseline = new LeoPathBaselineModel();
        return leoBaseline.Run(c).LatencyMs;
    }
}
