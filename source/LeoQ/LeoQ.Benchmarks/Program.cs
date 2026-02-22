using System.Globalization;
using System.Text.Json;
using LeoQ.Core.Abstractions;
using LeoQ.Core.Models;
using LeoQ.Core.Stats;
using LeoQ.Pqc.Models;
using LeoQ.Sim.Models;

static class Program
{
    // -----------------------------
    // DTO for JSON-driven scenarios
    // -----------------------------
    public sealed class ScenarioJob
    {
        public string ScenarioName { get; set; } = "demo";
        public double DistanceKm { get; set; } = 2200;
        public int HopCount { get; set; } = 12;
        public int Seed { get; set; } = 42;
        public int Runs { get; set; } = 2000;

        public double HandoverProb { get; set; } = 0.02;
        public double LeoAltitudeKm { get; set; } = 550.0;
        public int LeoIslHopCount { get; set; } = 4;

        // PQC knobs (optional)
        public bool PqcEnabled { get; set; } = true;
        public double PqcHandshakeMs { get; set; } = 4.0;
        public double PqcResumptionMs { get; set; } = 0.8;
        public double SessionResumptionProb { get; set; } = 0.85;
    }

    // -----------------------------
    // Main
    // -----------------------------
    public static int Main(string[] args)
    {
        var parsed = ParseArgs(args);

        // Output
        string outPath = Get(parsed, "--out", "results/day7_to_day12.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");

        // Manifest file for reproducibility evidence
        string manifestPath = Path.Combine(Path.GetDirectoryName(outPath) ?? ".", "run_manifest.json");

        // Scenario source
        string scenarioName = Get(parsed, "--scenario", "DFW-NYC");
        double distanceKm = GetDouble(parsed, "--distanceKm", 2200);
        int hops = GetInt(parsed, "--hops", 12);
        int seed = GetInt(parsed, "--seed", 42);
        int runs = GetInt(parsed, "--runs", 2000);
        double decisionSlaMs = GetDouble(parsed, "--decisionSlaMs", 30.0);
        double breachPenaltyAlpha = GetDouble(parsed, "--breachPenaltyAlpha", 0.0);

        double handoverProb = GetDouble(parsed, "--handoverProb", 0.02);

        // Optional sweeps
        var distances = GetDoubleList(parsed, "--distancesKm", "");
        var lambdaSweep = GetDoubleList(parsed, "--lambdaSweep", ""); // for LEOQ policy sensitivity

        // Optional JSON scenario file
        string scenarioFile = Get(parsed, "--scenarioFile", "");

        // Build jobs
        var jobs = LoadJobsOrDefault(
            scenarioFile,
            scenarioName,
            distanceKm,
            hops,
            seed,
            runs,
            handoverProb,
            distances
        );

        // Compose models (composition root is Benchmarks – correct)
        var pqc = new SimplePqcOverheadModel();

        // If lambdaSweep is provided, we’ll run PolicyRouter for each lambda.
        // If lambdaSweep is empty, we run default PolicyRouter once.
        var rows = new List<AggregateResultEx>();

        foreach (var job in jobs)
        {
            // Standard baselines
            var baselineModels = new List<ILatencyModel>
            {
                new FiberBaselineModel(),
                new InternetBaselineModel(),
                new LeoPathBaselineModel()
            };

            // Run baselines
            foreach (var model in baselineModels)
            {
                rows.Add(RunAggregate(job, model, model.Name, decisionSlaMs, breachPenaltyAlpha));
            }

            // Run LEOQ policy router(s)
            if (lambdaSweep.Count > 0)
            {
                foreach (var lambda in lambdaSweep)
                {
                    // NOTE: adjust this constructor signature if yours differs.
                    // Recommended signature: LeoQPolicyRouterModel(ICryptoOverheadModel crypto, double lambdaHandoverRisk = 3.0, double muCrypto = 1.0)
                    var policy = new LeoQPolicyRouterModel(pqc, lambdaHandoverRisk: lambda, muCrypto: 1.0);
                    rows.Add(RunAggregate(job, policy, $"LEOQ.PolicyRouter.lambda={lambda.ToString(CultureInfo.InvariantCulture)}", decisionSlaMs, breachPenaltyAlpha));
                }
            }
            else
            {
                var policy = new LeoQPolicyRouterModel(pqc, lambdaHandoverRisk: 3.0, muCrypto: 1.0);
                rows.Add(RunAggregate(job, policy, policy.Name, decisionSlaMs, breachPenaltyAlpha));
            }
        }

        // Write CSV (includes p50/p95/p99)
        WriteCsv(outPath, rows);

        // Write manifest JSON (EB-1A / reviewer-friendly reproducibility evidence)
        WriteManifest(manifestPath, args, jobs, distances, lambdaSweep, outPath);

        Console.WriteLine($"Wrote CSV: {outPath}");
        Console.WriteLine($"Wrote manifest: {manifestPath}");
        return 0;
    }

    // -----------------------------
    // Aggregate result with p99
    // (kept local to avoid forcing Core edits if you haven’t yet)
    // -----------------------------
    public sealed record AggregateResultEx(
    string ModelName,
    string ScenarioName,
    int Runs,
    double DistanceKm,
    int HopCount,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double Cvar99LatencyMs,
    double SlaBreachRate,
    double P50DecisionLatencyMs,
    double P95DecisionLatencyMs,
    double P99DecisionLatencyMs,
    double Cvar99DecisionLatencyMs
);

    private static AggregateResultEx RunAggregate(
     ScenarioJob job,
     ILatencyModel model,
     string modelNameOverride,
     double decisionSlaMs,
     double breachPenaltyAlpha)
    {
        var latencies = new List<double>(capacity: job.Runs);
        var decisionLatencies = new List<double>(capacity: job.Runs);

        int breaches = 0;

        for (int i = 0; i < job.Runs; i++)
        {
            var scenario = new ScenarioConfig(
                ScenarioName: job.ScenarioName,
                DistanceKm: job.DistanceKm,
                HopCount: job.HopCount,
                Seed: job.Seed + i,

                // LEO knobs
                LeoAltitudeKm: job.LeoAltitudeKm,
                LeoIslHopCount: job.LeoIslHopCount,
                HandoverProb: job.HandoverProb,

                // PQC knobs
                PqcEnabled: job.PqcEnabled,
                PqcHandshakeMs: job.PqcHandshakeMs,
                PqcResumptionMs: job.PqcResumptionMs,
                SessionResumptionProb: job.SessionResumptionProb
            );

            var r = model.Run(scenario);
            double L = r.LatencyMs;
            latencies.Add(L);

            if (L > decisionSlaMs) breaches++;

            // Finance-style “decision latency”: penalize lateness above SLA
            // alpha=0 => decisionLatency == latency (still fine)
            double penalty = breachPenaltyAlpha * Math.Max(0.0, L - decisionSlaMs);
            double decisionLatency = L + penalty;
            decisionLatencies.Add(decisionLatency);
        }

        // Network metrics
        double p50 = LatencyStats.Percentile(latencies, 50);
        double p95 = LatencyStats.Percentile(latencies, 95);
        double p99 = LatencyStats.Percentile(latencies, 99);
        double cvar99 = Cvar(latencies, 0.99);

        // Decision metrics
        double dp50 = LatencyStats.Percentile(decisionLatencies, 50);
        double dp95 = LatencyStats.Percentile(decisionLatencies, 95);
        double dp99 = LatencyStats.Percentile(decisionLatencies, 99);
        double dcvar99 = Cvar(decisionLatencies, 0.99);

        double breachRate = (job.Runs <= 0) ? 0.0 : (double)breaches / job.Runs;

        return new AggregateResultEx(
            ModelName: modelNameOverride,
            ScenarioName: job.ScenarioName,
            Runs: job.Runs,
            DistanceKm: job.DistanceKm,
            HopCount: job.HopCount,
            P50LatencyMs: p50,
            P95LatencyMs: p95,
            P99LatencyMs: p99,
            Cvar99LatencyMs: cvar99,
            SlaBreachRate: breachRate,
            P50DecisionLatencyMs: dp50,
            P95DecisionLatencyMs: dp95,
            P99DecisionLatencyMs: dp99,
            Cvar99DecisionLatencyMs: dcvar99
        );
    }


    private static double Cvar(List<double> values, double alpha)
    {
        if (values == null || values.Count == 0)
            throw new ArgumentException("Values must not be empty");

        if (alpha <= 0.0 || alpha >= 1.0)
            throw new ArgumentException("alpha must be in (0,1)");

        var sorted = values.OrderBy(v => v).ToArray();
        int n = sorted.Length;

        // CVaR_alpha = average of worst (1-alpha) tail
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

    private static void WriteCsv(string path, List<AggregateResultEx> rows)
    {
        using var sw = new StreamWriter(path, false);
        sw.WriteLine("model,scenario,runs,distance_km,hop_count," +
             "p50_latency_ms,p95_latency_ms,p99_latency_ms,cvar99_latency_ms,sla_breach_rate," +
             "p50_decision_latency_ms,p95_decision_latency_ms,p99_decision_latency_ms,cvar99_decision_latency_ms");
        foreach (var r in rows)
        {
            sw.WriteLine(string.Join(",",
     Escape(r.ModelName),
     Escape(r.ScenarioName),
     r.Runs.ToString(CultureInfo.InvariantCulture),
     r.DistanceKm.ToString(CultureInfo.InvariantCulture),
     r.HopCount.ToString(CultureInfo.InvariantCulture),
     r.P50LatencyMs.ToString("F6", CultureInfo.InvariantCulture),
     r.P95LatencyMs.ToString("F6", CultureInfo.InvariantCulture),
     r.P99LatencyMs.ToString("F6", CultureInfo.InvariantCulture),
     r.Cvar99LatencyMs.ToString("F6", CultureInfo.InvariantCulture),
     r.SlaBreachRate.ToString("F6", CultureInfo.InvariantCulture),
     r.P50DecisionLatencyMs.ToString("F6", CultureInfo.InvariantCulture),
     r.P95DecisionLatencyMs.ToString("F6", CultureInfo.InvariantCulture),
     r.P99DecisionLatencyMs.ToString("F6", CultureInfo.InvariantCulture),
     r.Cvar99DecisionLatencyMs.ToString("F6", CultureInfo.InvariantCulture)
 ));
        }
    }

    private static void WriteManifest(
        string manifestPath,
        string[] args,
        List<ScenarioJob> jobs,
        List<double> distances,
        List<double> lambdaSweep,
        string outPath)
    {
        var payload = new
        {
            utcTimestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            commandLine = args,
            outputCsv = outPath,
            distancesKm = distances,
            lambdaSweep = lambdaSweep,
            scenarios = jobs
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(manifestPath, json);
    }

    private static List<ScenarioJob> LoadJobsOrDefault(
        string scenarioFile,
        string scenarioName,
        double distanceKm,
        int hops,
        int seed,
        int runs,
        double handoverProb,
        List<double> distancesOverride)
    {
        // JSON file takes priority
        if (!string.IsNullOrWhiteSpace(scenarioFile) && File.Exists(scenarioFile))
        {
            var json = File.ReadAllText(scenarioFile);
            var jobs = JsonSerializer.Deserialize<List<ScenarioJob>>(json);
            return jobs ?? new List<ScenarioJob>();
        }

        // If distancesKm provided, create one job per distance
        if (distancesOverride.Count > 0)
        {
            return distancesOverride.Select(d => new ScenarioJob
            {
                ScenarioName = scenarioName,
                DistanceKm = d,
                HopCount = hops,
                Seed = seed,
                Runs = runs,
                HandoverProb = handoverProb
            }).ToList();
        }

        // Default single job
        return new List<ScenarioJob>
        {
            new ScenarioJob
            {
                ScenarioName = scenarioName,
                DistanceKm = distanceKm,
                HopCount = hops,
                Seed = seed,
                Runs = runs,
                HandoverProb = handoverProb
            }
        };
    }

    // -----------------------------
    // CLI parsing helpers
    // -----------------------------
    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--")) continue;

            string value = "true";
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                value = args[i + 1];
                i++; // consume
            }
            dict[key] = value;
        }
        return dict;
    }

    private static string Get(Dictionary<string, string> d, string k, string def)
        => d.TryGetValue(k, out var v) ? v : def;

    private static int GetInt(Dictionary<string, string> d, string k, int def)
        => d.TryGetValue(k, out var v) && int.TryParse(v, out var x) ? x : def;

    private static double GetDouble(Dictionary<string, string> d, string k, double def)
        => d.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : def;

    private static List<double> GetDoubleList(Dictionary<string, string> d, string k, string defCsv)
    {
        var raw = Get(d, k, defCsv);
        if (string.IsNullOrWhiteSpace(raw)) return new List<double>();

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(x => double.Parse(x, CultureInfo.InvariantCulture))
                  .ToList();
    }

    private static string Escape(string s)
        => "\"" + s.Replace("\"", "\"\"") + "\"";
}