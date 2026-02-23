# Leo-Q Framework

High-level simulation and analysis for low-earth-orbit latency & PQC studies.

See `docs/architecture` for architecture and class diagrams.

- Build status: ![CI](https://github.com/phaneendra520/leo-q-framework/actions/workflows/ci.yml/badge.svg)
- License: MIT

Quick start

```bash
dotnet build
dotnet test
```

Contributing

See `.github/CONTRIBUTING.md` and `.github/ISSUE_TEMPLATE` for how to contribute.

Sample runs

You can run the benchmark project with `dotnet run` and forward the sample arguments shown below. Results are written to the `results/` folder (create it if it does not exist).

Examples:

```bash
dotnet run --project LeoQ.Benchmarks -- --scenario "DFW-NYC" --distanceKm 2200 --hops 12 --seed 42 --out results/day2.csv

dotnet run --project LeoQ.Benchmarks -- --scenario "DFW-NYC" --distanceKm 2200 --hops 12 --seed 42 --runs 50 --out results/fiberbaseline.csv

dotnet run --project LeoQ.Benchmarks -- --scenario "DFW-NYC" --distanceKm 2200 --hops 12 --seed 42 --runs 200 --out results/leobaseline.csv

dotnet run --project LeoQ.Benchmarks -- --scenario "DFW-NYC" --distanceKm 2200 --hops 12 --seed 42 --runs 1000 --handoverProbPercent 0.08 --out results/PolicyrouterwithHandover.csv

dotnet run --project LeoQ.Benchmarks -- --scenario "Sweep" --distancesKm "500,1000,2200,5000,8000" --hops 12 --seed 42 --runs 2000 --handoverProb 0.08 --out results/sweep.csv

dotnet run --project LeoQ.Benchmarks -- --scenarioFile scenarios.json --out results/scenarios.csv

dotnet run --project LeoQ.Benchmarks -- --scenario "DFW-NYC" --distanceKm 2200 --hops 12 --seed 42 --runs 2000 --handoverProb 0.08 --lambdaSweep "0.5,1,2,3,5" --out results/policy_curve.csv

dotnet run --project LeoQ.Benchmarks -- --distanceKm 8000 --runs 4000 --handoverProb 0.10 --decisionSlaMs 65 --breachPenaltyAlpha 0 --lambdaSweep "1,2,3,5,8" --out results/bestcase_8000.csv
```

All generated CSV files and run artifacts are stored in the `LeoQ.Benchmarks/results/` directory when running from the repository root. Adjust paths when running from a different working directory.

Reproducible results

The following command reproduces the main long-haul experiment used in the paper and in the project results. It models long-haul LEO routing (8000 km) under moderate handover instability with compound spike behavior enabled.

```bash
dotnet run --project LeoQ.Benchmarks \
  --scenario "DFW-NYC" \
  --distanceKm 8000 \
  --hops 12 \
  --seed 42 \
  --runs 4000 \
  --handoverProb 0.10 \
  --decisionSlaMs 65 \
  --breachPenaltyAlpha 0 \
  --lambdaSweep "1,2,3,5,8" \
  --out results/bestcase_8000.csv
```

This configuration models long-haul LEO routing (8000 km) under moderate handover instability with compound spike behavior enabled.

Summary (example aggregated results)

| Model | p99 (ms) | CVaR99 (ms) | SLA Breach Rate |
|---|---:|---:|---:|
| Baseline LEO | 89.67 | 89.67 | 1.475% |
| LEO-Q | 51.67 | 63.74 | 0.275% |

Under compound handover instability, LEO-Q reduced p99 latency by 42% and decreased SLA violation rate by 5.3× compared to baseline LEO routing. LEO-Q does not optimize for raw median latency; instead, it reduces extreme tail events and improves decision reliability under instability.

Core statement

> **In long-haul routes with compound instability, LEO-Q demonstrates significant tail-risk reduction relative to baseline LEO routing.**

This claim is supported by the experiments and aggregated results included in this repository.
