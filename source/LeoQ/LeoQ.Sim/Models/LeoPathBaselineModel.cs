using System;
using System.Collections.Generic;
using System.Text;
using LeoQ.Core.Abstractions;
using LeoQ.Core.Models;
using LeoQ.Sim.Random;



namespace LeoQ.Sim.Models
{
    
    public sealed class LeoPathBaselineModel : ILatencyModel
    {
        public string Name => "Baseline.LEOPath";

        // LEO propagation uses speed of light in vacuum-ish; use 300 km/ms.
        private const double LightSpeedKmPerMs = 300.0;

        public RunResult Run(ScenarioConfig s)
        {
            var dr = new DeterministicRandom(s.Seed);

            // Estimate total path length (uplink + ISL + downlink)
            double pathKm = LeoPathGeometry.EstimatePathKm(s.DistanceKm, s.LeoAltitudeKm, s.LeoIslHopCount);

            // Propagation time
            double propagationMs = pathKm / LightSpeedKmPerMs;

            // Ground processing: uplink + downlink legs
            double groundProcMs = 2 * s.LeoGroundProcessingMs;

            // ISL hop overhead
            double islOverheadMs = s.LeoIslHopCount * s.LeoIslPerHopOverheadMs;

            // Handover event (rare, but creates tail)
            // We approximate Bernoulli(p) using Gaussian generator: convert to uniform via RNG.NextDouble in System.Random,
            // but our DeterministicRandom does not expose uniform, so we use Gaussian sign trick.
            // Simpler: derive a pseudo-uniform from Gaussian CDF-ish via clamp.
            double g = dr.NextGaussian(0, 1);
            double pseudoUniform = Math.Max(0.0, Math.Min(1.0, 0.5 + (g / 6.0))); // roughly [0,1]
            double handoverMs = (dr.NextUniform() < s.HandoverProb) ? s.HandoverPenaltyMs
       : 0.0;
            double latency = propagationMs + groundProcMs + islOverheadMs + handoverMs;

            return new RunResult(Name, s.ScenarioName, s.Seed, s.DistanceKm, s.HopCount, latency);
        }
    }

}
