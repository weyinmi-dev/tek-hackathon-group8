namespace Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

/// <summary>
/// Schema-validated AI output describing one detected anomaly. <see cref="Confidence"/>
/// is in [0, 1]; the decision engine drops anomalies below the actionability threshold.
/// </summary>
public sealed record DetectedAnomaly(
    string TowerCode,
    AnomalyType Type,
    PipelineAlertSeverity Severity,
    decimal Confidence,
    DateTimeOffset DetectedAt,
    string Explanation,
    IReadOnlyDictionary<string, decimal> Metrics);

public sealed record ProposedOptimization(
    string TowerCode,
    OptimizationType Type,
    decimal EstimatedImpact,
    string Rationale);
