# Baseline characterization harness (Phase 4 / M1)

The safety net for the Semantic Kernel → Microsoft Agent Framework migration.
See [Phase 3 §5.3](../../docs/PHASE3_MIGRATION_PLAN.md) for why this exists.

Because the test project fix was deferred to M15, **this harness is the sole
automated check that the network-ingestion pipeline still produces identical
output after each cutover.** It captures the six parity-contract counts —
`eventsParsed`, `anomaliesDetected`, `alertsCreated`, `alertsUpdated`,
`optimizationsCreated`, `topologyChanged` (plus `finalStatus`) — from the
current code and diffs them against committed golden files.

## Why offline mode is a valid baseline

`appsettings.json` ships `Ai:Provider = "Mock"`. In that mode the pipeline
computes anomalies from fixed thresholds (`HeuristicNetworkBatchAnalyzer`),
so the same fixture always yields the same counts. The migration moves those
thresholds into `AnomalyThresholdPolicy` (M3/M12) **without changing the
numbers**. The prose the copilot writes will change; these counts must not.

## The content-hash dedup trap — read before running

The pipeline is content-hash idempotent. Re-ingesting the same file
short-circuits to the original run's cached counts **without re-executing the
analyzer**. A replay against a database that already saw these fixtures would
pass trivially and give false confidence.

**Every capture and every replay must run against a database in which these
fixtures have not yet been ingested.** `capture.ps1` throws if it detects a
deduplicated run, so you cannot record a poisoned baseline by accident — but
you still have to start clean:

```powershell
docker compose down -v      # wipe volumes  (or delete the Aspire pg data volume)
dotnet run --project src/AppHost   # bring the stack back up, fresh DB
```

## Usage

```powershell
# 1. Establish the baseline from the CURRENT (pre-migration) code, fresh DB:
pwsh scripts/baseline/capture.ps1
#    -> writes docs/baselines/ingest-*.json   (commit these)

# 2. After each cutover milestone, on a fresh DB, prove parity held:
pwsh scripts/baseline/replay.ps1
#    MATCH  -> counts unchanged
#    DIFF   -> a milestone changed pipeline output; review before proceeding
```

Default `-BaseUrl` is `http://localhost:5000` (Web.Api under Aspire). Override
if your stack binds elsewhere. Admin credentials default to the seeded
`tunde.b@telco.lag` / `Telco!2025`.

## Fixtures

| File | Exercises | Expected (offline mode) |
| --- | --- | --- |
| `fixtures/network-log-small.csv` | one tower, all thresholds breached | signal-drop + load-spike + latency anomalies, a load-balance optimization, topology status changes |
| `fixtures/network-log-topology.csv` | two towers, sub-threshold metrics, status changes only | topology status changes, no metric anomalies |

The golden files record what the pipeline actually produced — capture them once
against the current build, then treat them as fixed.

## Two harnesses

**Primary — `tools/BaselineCapture` (in-process, Testcontainers).** The verified
parity gate. Spins a fresh `postgres:17.6` container per run and drives the real
`ProcessNetworkLogCommand` pipeline in-process, so the content-hash dedup never
fires and each run genuinely re-executes the analyzer. Golden files live in
`docs/baselines/`. Requires Docker; the network pipeline needs no `vector`
extension, so plain `postgres:17.6` suffices.

```powershell
dotnet run --project tools/BaselineCapture -- capture   # establish the baseline
dotnet run --project tools/BaselineCapture -- verify    # after each milestone; exit 1 on drift
```

**Alternative — the PowerShell scripts here (HTTP, live stack).** Exercise the
pipeline over HTTP against a running Aspire stack. Useful as an integration-level
smoke test, but subject to the dedup trap above, so they require a fresh database
each run. Prefer the in-process harness for the parity gate.
