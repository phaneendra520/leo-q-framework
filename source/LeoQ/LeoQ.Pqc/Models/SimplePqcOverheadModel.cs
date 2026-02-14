using System;
using System.Collections.Generic;
using System.Text;
using LeoQ.Core.Abstractions;
using LeoQ.Core.Models;
using LeoQ.Core.Random;

namespace LeoQ.Pqc.Models
{
    
    public sealed class SimplePqcOverheadModel : ICryptoOverheadModel
    {
        public string Name => "PQC.Simple";

        public double SampleCryptoOverheadMs(ScenarioConfig s)
        {
            if (!s.PqcEnabled) return 0.0;

            // Deterministic decision per run based on seed:
            var dr = new DeterministicRandom(s.Seed);

            // If session resumption hits, overhead is small; else handshake is larger.
            bool resumed = dr.NextUniform() < s.SessionResumptionProb;

            return resumed ? s.PqcResumptionMs : s.PqcHandshakeMs;
        }
    }

}
