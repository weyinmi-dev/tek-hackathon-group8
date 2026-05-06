using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline;

/// <summary>
/// Deterministic, dependency-free analyzer used when Azure OpenAI is not configured
/// (the same fallback shape <c>MockCopilotOrchestrator</c> uses for chat). Applies
/// fixed thresholds to the raw events and produces a plausible
/// <see cref="AiAnalysisResult"/> — same input always yields the same output, which
/// makes integration tests deterministic and lets the demo run without an API key.
///
/// The thresholds intentionally err on the side of producing at least one anomaly
/// when the data calls for it, so the success criteria ("≥1 anomaly, ≥1 alert,
/// dashboard updated" from the brief) are reachable in offline mode.
/// </summary>
internal sealed class HeuristicNetworkBatchAnalyzer : INetworkBatchAnalyzer
{
    public Task<Result<AiAnalysisResult>> AnalyzeAsync(
        Guid ingestionRunId,
        IReadOnlyList<NetworkEvent> events,
        string? mcpFilePath = null,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Task.FromResult(Result.Success(AiAnalysisResult.Empty));
        }

        IEnumerable<IGrouping<string, NetworkEvent>> byTower = events
            .GroupBy(e => e.TowerCode, StringComparer.OrdinalIgnoreCase);

        var anomalies = new List<DetectedAnomaly>();
        var optimizations = new List<ProposedOptimization>();
        var statusChanges = new List<TowerStatusChange>();
        var metricUpdates = new List<TowerMetricUpdate>();

        foreach (IGrouping<string, NetworkEvent> tower in byTower)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvaluateTower(tower.Key, [.. tower.OrderBy(e => e.OccurredAt)], anomalies, optimizations, statusChanges, metricUpdates);
        }

        TopologyDelta? topology = (statusChanges.Count == 0 && metricUpdates.Count == 0)
            ? null
            : new TopologyDelta(statusChanges, metricUpdates);

        return Task.FromResult(Result.Success(new AiAnalysisResult(anomalies, optimizations, topology)));
    }

    private static void EvaluateTower(
        string towerCode,
        IReadOnlyList<NetworkEvent> events,
        List<DetectedAnomaly> anomalies,
        List<ProposedOptimization> optimizations,
        List<TowerStatusChange> statusChanges,
        List<TowerMetricUpdate> metricUpdates)
    {
        NetworkEvent first = events[0];
        NetworkEvent last = events[^1];

        // ── Anomaly: signal drop ──────────────────────────────────────────
        // Detect when a tower's signal collapses across the batch window.
        if (first.SignalPct is int firstSignal &&
            last.SignalPct is int lastSignal &&
            firstSignal - lastSignal >= 30)
        {
            decimal confidence = Math.Min(1m, (firstSignal - lastSignal) / 60m + 0.5m);
            PipelineAlertSeverity severity = lastSignal < 40
                ? PipelineAlertSeverity.Critical
                : PipelineAlertSeverity.Warn;

            anomalies.Add(new DetectedAnomaly(
                TowerCode: towerCode,
                Type: AnomalyType.SignalDrop,
                Severity: severity,
                Confidence: confidence,
                DetectedAt: last.OccurredAt,
                Explanation: $"Signal fell from {firstSignal}% to {lastSignal}% across {events.Count} samples.",
                Metrics: new Dictionary<string, decimal>
                {
                    ["signalStart"] = firstSignal,
                    ["signalEnd"] = lastSignal
                }));
        }

        // ── Anomaly: load spike ───────────────────────────────────────────
        if (events.Any(e => e.LoadPct is >= 85))
        {
            int peak = events.Where(e => e.LoadPct is not null).Max(e => e.LoadPct!.Value);
            anomalies.Add(new DetectedAnomaly(
                TowerCode: towerCode,
                Type: AnomalyType.LoadSpike,
                Severity: peak >= 95 ? PipelineAlertSeverity.Critical : PipelineAlertSeverity.Warn,
                Confidence: peak >= 95 ? 0.95m : 0.75m,
                DetectedAt: last.OccurredAt,
                Explanation: $"Load reached {peak}% (sustained ≥85% threshold).",
                Metrics: new Dictionary<string, decimal> { ["loadPeak"] = peak }));

            optimizations.Add(new ProposedOptimization(
                TowerCode: towerCode,
                Type: OptimizationType.LoadBalance,
                EstimatedImpact: peak >= 95 ? 0.7m : 0.4m,
                Rationale: $"Tower {towerCode} sustained {peak}% load — shed traffic to neighboring sites."));
        }

        // ── Anomaly: latency anomaly ──────────────────────────────────────
        if (events.Any(e => e.LatencyMs is >= 100))
        {
            int peak = events.Where(e => e.LatencyMs is not null).Max(e => e.LatencyMs!.Value);
            anomalies.Add(new DetectedAnomaly(
                TowerCode: towerCode,
                Type: AnomalyType.LatencyAnomaly,
                Severity: peak >= 200 ? PipelineAlertSeverity.Critical : PipelineAlertSeverity.Warn,
                Confidence: 0.7m,
                DetectedAt: last.OccurredAt,
                Explanation: $"Latency peaked at {peak}ms (≥100ms threshold).",
                Metrics: new Dictionary<string, decimal> { ["latencyPeakMs"] = peak }));
        }

        // ── Topology: status change ───────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(first.RawStatus) &&
            !string.IsNullOrWhiteSpace(last.RawStatus) &&
            !string.Equals(first.RawStatus, last.RawStatus, StringComparison.OrdinalIgnoreCase))
        {
            statusChanges.Add(new TowerStatusChange(
                TowerCode: towerCode,
                PreviousStatus: first.RawStatus!,
                NewStatus: last.RawStatus!,
                Reason: "Status differs between earliest and latest sample in batch."));
        }

        // ── Topology: metric update ───────────────────────────────────────
        // Only emit when the latest sample has metrics; lets the decision engine
        // do the threshold comparison against the live tower snapshot.
        if (last.SignalPct is not null || last.LoadPct is not null || last.LatencyMs is not null)
        {
            metricUpdates.Add(new TowerMetricUpdate(
                TowerCode: towerCode,
                SignalPct: last.SignalPct,
                LoadPct: last.LoadPct,
                LatencyMs: last.LatencyMs));
        }
    }
}
