using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Pure rule-based translator from validated AI output to a sequence of side-effecting
/// pipeline actions. No DbContext, no Semantic Kernel, no clock, no logger — by
/// construction trivially unit-testable. The engine never executes the actions; that
/// is Stage 4's job.
/// </summary>
public sealed class DefaultDecisionEngine(DecisionEngineOptions options) : IDecisionEngine
{
    private readonly DecisionEngineOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public IReadOnlyList<PipelineAction> Decide(
        AiAnalysisResult ai,
        IReadOnlyList<AlertSnapshot> activeAlerts,
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers)
    {
        ArgumentNullException.ThrowIfNull(ai);
        ArgumentNullException.ThrowIfNull(activeAlerts);
        ArgumentNullException.ThrowIfNull(currentTowers);

        var actions = new List<PipelineAction>();

        // ── Anomalies ───────────────────────────────────────────────────────────
        // 1. Drop low-confidence noise.
        // 2. Within-batch dedupe by fingerprint (AI can produce two signals for the
        //    same tower+type+time-bucket; collapse them keeping the most actionable).
        // 3. Look up live alerts by fingerprint:
        //      - match found, not resolved → UpdateAlertAction
        //      - match resolved, or no match → CreateAlertAction
        Dictionary<string, DetectedAnomaly> anomalyByFingerprint = DedupeAnomalies(ai.Anomalies);
        Dictionary<string, AlertSnapshot> liveByFingerprint = activeAlerts
            .Where(a => !a.IsResolved)
            .GroupBy(a => a.AnomalyFingerprint, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var anomalyFingerprintsByTower = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach ((string fingerprint, DetectedAnomaly anomaly) in anomalyByFingerprint)
        {
            if (liveByFingerprint.TryGetValue(fingerprint, out AlertSnapshot? existing))
            {
                actions.Add(new UpdateAlertAction(fingerprint, existing.Id, anomaly));
            }
            else
            {
                actions.Add(new CreateAlertAction(fingerprint, anomaly));
            }

            // Remember the first/winning fingerprint per tower so optimizations
            // emitted for that tower can correlate to the same alert.
            anomalyFingerprintsByTower.TryAdd(NormalizeTowerCode(anomaly.TowerCode), fingerprint);
        }

        // ── Optimizations ──────────────────────────────────────────────────────
        // Filter by impact. Correlate to an anomaly fingerprint of the same tower
        // when one exists in this batch — gives downstream views a way to thread
        // recommendations back to the alert that motivated them.
        foreach (ProposedOptimization optimization in ai.Optimizations)
        {
            if (optimization.EstimatedImpact < _options.MinOptimizationImpact)
            {
                continue;
            }

            string correlated = anomalyFingerprintsByTower.TryGetValue(
                NormalizeTowerCode(optimization.TowerCode), out string? fingerprint)
                ? fingerprint
                : string.Empty;

            actions.Add(new CreateOptimizationAction(correlated, optimization));
        }

        // ── Topology ───────────────────────────────────────────────────────────
        // Defensive: only emit updates for towers we already know about. The AI
        // is not allowed to create towers — that's an operator action.
        // Status changes and metric updates for the same tower are merged into a
        // single UpdateTowerAction so Stage 4 commits one update.
        if (ai.Topology is not null)
        {
            actions.AddRange(BuildTowerActions(ai.Topology, currentTowers));
        }

        return actions;
    }

    private Dictionary<string, DetectedAnomaly> DedupeAnomalies(IReadOnlyList<DetectedAnomaly> anomalies)
    {
        var byFingerprint = new Dictionary<string, DetectedAnomaly>(StringComparer.OrdinalIgnoreCase);

        foreach (DetectedAnomaly anomaly in anomalies)
        {
            if (anomaly.Confidence < _options.MinAnomalyConfidence)
            {
                continue;
            }

            string fingerprint = Fingerprints.AnomalyFingerprint(
                anomaly.TowerCode,
                anomaly.Type.ToString(),
                anomaly.DetectedAt,
                _options.EffectiveAnomalyTimeBucket);

            if (!byFingerprint.TryGetValue(fingerprint, out DetectedAnomaly? existing) ||
                IsMoreActionable(anomaly, existing))
            {
                byFingerprint[fingerprint] = anomaly;
            }
        }

        return byFingerprint;
    }

    private static bool IsMoreActionable(DetectedAnomaly candidate, DetectedAnomaly incumbent)
    {
        // Higher severity wins. Tie → higher confidence wins. Tie → keep incumbent
        // (stable iteration so the engine is deterministic).
        if (candidate.Severity != incumbent.Severity)
        {
            return candidate.Severity > incumbent.Severity;
        }

        return candidate.Confidence > incumbent.Confidence;
    }

    private IEnumerable<UpdateTowerAction> BuildTowerActions(
        TopologyDelta topology,
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers)
    {
        var statusByTower = new Dictionary<string, TowerStatusChange>(StringComparer.OrdinalIgnoreCase);
        foreach (TowerStatusChange change in topology.StatusChanges)
        {
            string code = NormalizeTowerCode(change.TowerCode);
            if (!currentTowers.TryGetValue(code, out TowerSnapshot? current))
            {
                continue; // unknown tower → defensively skip
            }

            if (string.Equals(current.Status, change.NewStatus, StringComparison.OrdinalIgnoreCase))
            {
                continue; // no-op: AI re-asserted the current state
            }

            statusByTower[code] = change;
        }

        var metricByTower = new Dictionary<string, TowerMetricUpdate>(StringComparer.OrdinalIgnoreCase);
        foreach (TowerMetricUpdate metric in topology.MetricUpdates)
        {
            string code = NormalizeTowerCode(metric.TowerCode);
            if (!currentTowers.TryGetValue(code, out TowerSnapshot? current))
            {
                continue;
            }

            if (!HasMaterialMetricDelta(current, metric))
            {
                continue;
            }

            metricByTower[code] = metric;
        }

        // Merge: union of tower codes that had either a status or metric change.
        IEnumerable<string> codes = statusByTower.Keys.Union(metricByTower.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (string code in codes)
        {
            statusByTower.TryGetValue(code, out TowerStatusChange? status);
            metricByTower.TryGetValue(code, out TowerMetricUpdate? metric);
            yield return new UpdateTowerAction(code, status, metric);
        }
    }

    private bool HasMaterialMetricDelta(TowerSnapshot current, TowerMetricUpdate update)
    {
        if (update.SignalPct is int signal &&
            Math.Abs(signal - current.SignalPct) > _options.MetricDeltaPercentThreshold)
        {
            return true;
        }

        if (update.LoadPct is int load &&
            Math.Abs(load - current.LoadPct) > _options.MetricDeltaPercentThreshold)
        {
            return true;
        }

        // Latency isn't on the snapshot today — any non-null value is reported
        // until/unless we extend TowerSnapshot. Bounded by the absolute threshold.
        if (update.LatencyMs is int latency && latency >= _options.LatencyDeltaMsThreshold)
        {
            return true;
        }

        return false;
    }

    private static string NormalizeTowerCode(string code) => code.Trim().ToUpperInvariant();
}
