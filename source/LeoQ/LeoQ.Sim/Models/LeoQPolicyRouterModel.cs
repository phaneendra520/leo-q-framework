using LeoQ.Core.Abstractions;
using LeoQ.Core.Models;
using LeoQ.Pqc.Models;

namespace LeoQ.Sim.Models;

public sealed class LeoQPolicyRouterModel : ILatencyModel
{
    public string Name => "LEOQ.PolicyRouter";

    // Tuning weights (keep simple and explicit)
    private const double LightSpeedKmPerMs = 300.0;
    private const int PolicyEvalSamples = 64;        // small internal sample per candidate
    private const double RiskWeight = 150.0;         // scales tail risk vs mean (tune if needed)
    private const double CvarAlpha = 0.99;           // tail metric inside policy
    private readonly ICryptoOverheadModel _crypto;
    private readonly double _lambdaHandoverRisk;
    private readonly double _muCrypto;

    // Default constructor (keeps backward compatibility)
    public LeoQPolicyRouterModel()
        : this(new SimplePqcOverheadModel(), 3.0, 1.0)
    {
    }

    // Constructor with crypto only (keeps old usage working)
    public LeoQPolicyRouterModel(ICryptoOverheadModel crypto)
        : this(crypto, 3.0, 1.0)
    {
    }

    // Full constructor (used for lambda sweep / sensitivity analysis)
    public LeoQPolicyRouterModel(
        ICryptoOverheadModel crypto,
        double lambdaHandoverRisk,
        double muCrypto)
    {
        _crypto = crypto;
        _lambdaHandoverRisk = lambdaHandoverRisk;
        _muCrypto = muCrypto;
    }

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
            HandoverProb = Math.Max(0.005, s.HandoverProb * 0.15)
        };


        double fastScore = ScoreCandidateTailAware(fast);
        double stableScore = ScoreCandidateTailAware(stable);

        var chosen = (stableScore <= fastScore) ? stable : fast;

        // Now “execute” latency sample using the same LEO path logic + crypto overhead
        double latency = SampleLeoLatencyMs(chosen) + _crypto.SampleCryptoOverheadMs(chosen);

        return new RunResult(Name, chosen.ScenarioName, chosen.Seed, chosen.DistanceKm, chosen.HopCount, latency);
    }

    private double ScoreCandidateTailAware(ScenarioConfig c)
    {
        // Internal quick sampling to estimate tail risk & SLA breach probability
        // This is your "controller" behavior: choose stability when risk increases.
        var samples = new List<double>(PolicyEvalSamples);
        int breaches = 0;

        for (int i = 0; i < PolicyEvalSamples; i++)
        {
            var ci = c with { Seed = c.Seed + 10_000 + i }; // deterministic offset
            double L = SampleLeoLatencyMs(ci) + _crypto.SampleCryptoOverheadMs(ci);
            samples.Add(L);

            if (L > c.DecisionSlaMs) breaches++;
        }

        double mean = samples.Average();
        double cvar = Cvar(samples, CvarAlpha);
        double breachRate = (double)breaches / PolicyEvalSamples;

        // Score: expected latency + weighted tail risk + weighted SLA breach risk
        // Breach rate is multiplied to make it comparable in ms-units.
        return mean + RiskWeight * cvar + (RiskWeight * 100.0) * breachRate;
    }

    private static double Cvar(List<double> values, double alpha)
    {
        if (values.Count == 0) return 0.0;
        var sorted = values.OrderBy(v => v).ToArray();
        int n = sorted.Length;

        int startIndex = (int)Math.Ceiling(alpha * n);
        if (startIndex >= n) startIndex = n - 1;

        double sum = 0.0;
        int count = 0;

        for (int i = startIndex; i < n; i++)
        {
            sum += sorted[i];
            count++;
        }

        return sum / Math.Max(1, count);
    }

    private double ScoreCandidate(ScenarioConfig c)
    {
        // Use expected latency (no random handover) + expected handover penalty + expected crypto overhead
        double expectedLeo = ExpectedLeoLatencyMs(c);
        double expectedHandover = _lambdaHandoverRisk * (c.HandoverProb * c.HandoverPenaltyMs);

        // Expected crypto overhead approximated by weighted average:
        double expectedCrypto = (!c.PqcEnabled) ? 0.0
            : _muCrypto * (c.SessionResumptionProb * c.PqcResumptionMs + (1.0 - c.SessionResumptionProb) * c.PqcHandshakeMs);

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
