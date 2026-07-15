using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.UnitTests.Ingestion.Decisions;

/// <summary>
/// Builders for AI-output and current-state fixtures so the engine tests read
/// like specs rather than long object-literal walls.
/// </summary>
internal static class DecisionFixtures
{
    public static readonly DateTimeOffset Ts = new(2026, 5, 5, 8, 7, 0, TimeSpan.Zero);

    public static DetectedAnomaly Anomaly(
        string tower = "LOS-T-014",
        AnomalyType type = AnomalyType.SignalDrop,
        PipelineAlertSeverity severity = PipelineAlertSeverity.Warn,
        decimal confidence = 0.8m,
        DateTimeOffset? at = null) =>
        new(
            TowerCode: tower,
            Type: type,
            Severity: severity,
            Confidence: confidence,
            DetectedAt: at ?? Ts,
            Explanation: "test fixture",
            Metrics: new Dictionary<string, decimal>());

    public static ProposedOptimization Optimization(
        string tower = "LOS-T-014",
        OptimizationType type = OptimizationType.LoadBalance,
        decimal impact = 0.6m) =>
        new(tower, type, impact, "test fixture");

    public static AiAnalysisResult AiResult(
        IEnumerable<DetectedAnomaly>? anomalies = null,
        IEnumerable<ProposedOptimization>? optimizations = null,
        TopologyDelta? topology = null) =>
        new(
            (anomalies ?? []).ToList(),
            (optimizations ?? []).ToList(),
            topology);

    public static AlertSnapshot ActiveAlert(
        string fingerprint,
        Guid? id = null,
        PipelineAlertSeverity severity = PipelineAlertSeverity.Warn,
        bool resolved = false,
        string towerCode = "LOS-T-014") =>
        new(
            Id: id ?? Guid.NewGuid(),
            AnomalyFingerprint: fingerprint,
            Severity: severity,
            LastSeenAt: Ts,
            OccurrenceCount: 1,
            IsResolved: resolved,
            TowerCode: towerCode);

    public static TowerSnapshot Tower(
        string code = "LOS-T-014",
        string region = "Lagos West",
        string status = "ok",
        int signalPct = 90,
        int loadPct = 50) =>
        new(code, region, status, signalPct, loadPct);

    public static IReadOnlyDictionary<string, TowerSnapshot> Towers(params TowerSnapshot[] snapshots) =>
        snapshots.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

    public static string FingerprintFor(DetectedAnomaly anomaly, TimeSpan? bucket = null) =>
        Fingerprints.AnomalyFingerprint(
            anomaly.TowerCode,
            anomaly.Type.ToString(),
            anomaly.DetectedAt,
            bucket);
}
