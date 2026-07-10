using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Application.Ingestion.Stage2_Analyze;

/// <summary>
/// The Network module's deterministic anomaly-classification rules — extracted from the
/// AI infrastructure layer, where they never belonged (Phase 1 §4.9, Phase 2 D10/§12).
/// These fixed thresholds define what counts as a signal drop, load spike, latency anomaly
/// or topology change; they are business policy, not model output.
///
/// Lives in Network.Application rather than Network.Domain because it produces the Stage-2
/// contract records (<see cref="DetectedAnomaly"/> etc.), which are Application-layer types.
/// When those contracts move to the AI module (Phase 3 M12) the policy stays here as the
/// single source of truth: the deterministic offline analyzer and the MAF workflow's
/// threshold pre-filter (Phase 2 §7.2) both call it, so obvious breaches never cost an LLM.
///
/// Logic is a verbatim lift of the former <c>HeuristicNetworkBatchAnalyzer.EvaluateTower</c>;
/// the magic numbers are the only thing named here, so behaviour is unchanged (the
/// characterization harness in tools/BaselineCapture verifies the pipeline counts hold).
/// </summary>
public static class AnomalyThresholdPolicy
{
    /// <summary>Minimum signal-percentage drop across the batch window to flag a SignalDrop.</summary>
    public const int SignalDropThreshold = 30;

    /// <summary>Signal percentage below which a signal drop is Critical rather than Warn.</summary>
    public const int SignalCriticalFloor = 40;

    /// <summary>Load percentage at or above which a LoadSpike is flagged.</summary>
    public const int LoadSpikeThreshold = 85;

    /// <summary>Load percentage at or above which a load spike is Critical rather than Warn.</summary>
    public const int LoadCriticalThreshold = 95;

    /// <summary>Latency in ms at or above which a LatencyAnomaly is flagged.</summary>
    public const int LatencyThreshold = 100;

    /// <summary>Latency in ms at or above which a latency anomaly is Critical rather than Warn.</summary>
    public const int LatencyCriticalThreshold = 200;

    /// <summary>
    /// Classifies a single tower's chronologically-ordered events, appending any detected
    /// anomalies, optimizations and topology deltas to the supplied collections.
    /// </summary>
    public static void EvaluateTower(
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
        if (first.SignalPct is int firstSignal &&
            last.SignalPct is int lastSignal &&
            firstSignal - lastSignal >= SignalDropThreshold)
        {
            decimal confidence = Math.Min(1m, (firstSignal - lastSignal) / 60m + 0.5m);
            PipelineAlertSeverity severity = lastSignal < SignalCriticalFloor
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
        if (events.Any(e => e.LoadPct is not null && e.LoadPct >= LoadSpikeThreshold))
        {
            int peak = events.Where(e => e.LoadPct is not null).Max(e => e.LoadPct!.Value);
            anomalies.Add(new DetectedAnomaly(
                TowerCode: towerCode,
                Type: AnomalyType.LoadSpike,
                Severity: peak >= LoadCriticalThreshold ? PipelineAlertSeverity.Critical : PipelineAlertSeverity.Warn,
                Confidence: peak >= LoadCriticalThreshold ? 0.95m : 0.75m,
                DetectedAt: last.OccurredAt,
                Explanation: $"Load reached {peak}% (sustained ≥{LoadSpikeThreshold}% threshold).",
                Metrics: new Dictionary<string, decimal> { ["loadPeak"] = peak }));

            optimizations.Add(new ProposedOptimization(
                TowerCode: towerCode,
                Type: OptimizationType.LoadBalance,
                EstimatedImpact: peak >= LoadCriticalThreshold ? 0.7m : 0.4m,
                Rationale: $"Tower {towerCode} sustained {peak}% load — shed traffic to neighboring sites."));
        }

        // ── Anomaly: latency anomaly ──────────────────────────────────────
        if (events.Any(e => e.LatencyMs is not null && e.LatencyMs >= LatencyThreshold))
        {
            int peak = events.Where(e => e.LatencyMs is not null).Max(e => e.LatencyMs!.Value);
            anomalies.Add(new DetectedAnomaly(
                TowerCode: towerCode,
                Type: AnomalyType.LatencyAnomaly,
                Severity: peak >= LatencyCriticalThreshold ? PipelineAlertSeverity.Critical : PipelineAlertSeverity.Warn,
                Confidence: 0.7m,
                DetectedAt: last.OccurredAt,
                Explanation: $"Latency peaked at {peak}ms (≥{LatencyThreshold}ms threshold).",
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
