namespace LeoQ.Core.Models;

public sealed record ScenarioConfig(
    string ScenarioName,
    double DistanceKm,
    int HopCount,
    int Seed,
    double FiberSpeedKmPerMs = 200.0,
    double BaseInternetOverheadMs = 8.0,
    double JitterStdDevMs = 1.5,
    double LeoAltitudeKm = 550.0,          // typical LEO altitude (configurable)
    int LeoIslHopCount = 4,                // crosslink hops
    double LeoGroundProcessingMs = 2.0,     // ground station + modem processing per leg
    double LeoIslPerHopOverheadMs = 0.4,    // switching/queue per ISL hop
    double HandoverProb = 0.02,             // chance of handover event per run
    double HandoverPenaltyMs = 8.0,          // extra latency if handover occurs
    bool PqcEnabled = true,
    double PqcHandshakeMs = 4.0,         // worst-case handshake overhead
    double PqcResumptionMs = 0.8,        // session resumption overhead
    double SessionResumptionProb = 0.85  // probability resumption works

);
