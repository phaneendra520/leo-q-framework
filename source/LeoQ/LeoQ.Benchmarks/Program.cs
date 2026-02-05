using System.Globalization;
using LeoQ.Core.Models;
using LeoQ.Core.Abstractions;
using LeoQ.Sim.Models;

static class Program
{
    public static int Main(string[] args)
    {
        var parsed = ParseArgs(args);

        string scenarioName = Get(parsed, "--scenario", "demo");
        double distanceKm = GetDouble(parsed, "--distanceKm", 5500);
        int hops = GetInt(parsed, "--hops", 14);
        int seed = GetInt(parsed, "--seed", 42);
        string outPath = Get(parsed, "--out", "results/day2_results.csv");

        var scenario = new ScenarioConfig(scenarioName, distanceKm, hops, seed);

        ILatencyModel[] models = new ILatencyModel[]
        {
            new FiberBaselineModel(),
            new InternetBaselineModel()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");

        var results = models.Select(m => m.Run(scenario)).ToList();

        foreach (var r in results)
            Console.WriteLine($"{r.ModelName} latency_ms={r.LatencyMs:F3}");

        WriteCsv(outPath, results);
        Console.WriteLine($"Wrote: {outPath}");
        return 0;
    }

    static void WriteCsv(string path, List<RunResult> rows)
    {
        using var sw = new StreamWriter(path, false);
        sw.WriteLine("model,scenario,seed,distance_km,hop_count,latency_ms");

        foreach (var r in rows)
        {
            sw.WriteLine(string.Join(",",
                r.ModelName,
                r.ScenarioName,
                r.Seed,
                r.DistanceKm.ToString(CultureInfo.InvariantCulture),
                r.HopCount,
                r.LatencyMs.ToString("F6", CultureInfo.InvariantCulture)
            ));
        }
    }

    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            string val = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[i+1] : "true";
            dict[args[i]] = val;
        }
        return dict;
    }

    static string Get(Dictionary<string, string> d, string k, string def)
        => d.TryGetValue(k, out var v) ? v : def;

    static int GetInt(Dictionary<string, string> d, string k, int def)
        => d.TryGetValue(k, out var v) && int.TryParse(v, out var x) ? x : def;

    static double GetDouble(Dictionary<string, string> d, string k, double def)
        => d.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : def;
}
