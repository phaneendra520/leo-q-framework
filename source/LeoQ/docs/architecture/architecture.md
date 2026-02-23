# Architecture Overview

This document contains high-level architecture diagrams for the `leo-q-framework` repository. The diagrams are provided as both Mermaid (GitHub-friendly) and PlantUML sources so contributors can view or render them locally or on CI.

## Mermaid component diagram

```mermaid
flowchart TB
  subgraph Repository
    direction TB
    Core["LEOQ.Core\n(models, abstractions, stats)"]
    Sim["LeoQ.Sim\n(simulation models & routers)"]
    Pqc["LeoQ.Pqc\n(PQC overhead models)"]
    Bench["LeoQ.Benchmarks\n(bench runner & CLI)"]
    Risk["LeoQ.Risk\n(risk analytics)"]
    Tests["Tests\n(unit tests)"]
  end

  Bench --> Sim
  Bench --> Core
  Sim --> Core
  Sim --> Pqc
  Risk --> Core
  Tests --> Core
  Tests --> Sim

  %% Notes
  classDef components fill:#f8f9fa,stroke:#333,stroke-width:1px;
  class Core,Sim,Pqc,Bench,Risk,Tests components;
```

## Mermaid sequence example (bench run)

```mermaid
sequenceDiagram
  participant User
  participant Bench as LeoQ.Benchmarks
  participant Sim as LeoQ.Sim
  participant Core as LEOQ.Core
  participant Pqc as LeoQ.Pqc

  User->>Bench: run benchmark (scenario config)
  Bench->>Sim: execute simulation
  Sim->>Core: use models & abstractions (ScenarioConfig, geometry)
  Sim->>Pqc: request PQC overhead estimates
  Pqc-->>Sim: return overhead
  Sim-->>Bench: simulation results
  Bench->>User: report results
```

## Files included
- `architecture.puml` — PlantUML source for the component diagram (in same folder).
- `architecture.md` — this rendered Markdown with embedded Mermaid diagrams (GitHub renders Mermaid blocks on supported pages).
- `README.md` — instructions for rendering PlantUML locally or using online renderers.

If you want additional diagrams (deployment, class-level, sequence per feature) tell me which area to expand.
