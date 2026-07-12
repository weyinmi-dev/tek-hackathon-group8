using Application.Abstractions.Pipeline;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Discriminated union of every side-effecting action the persistence stage may
/// dispatch. The decision engine returns these as plain data; the persistence
/// stage executes them via the owning module's MediatR command. AI never produces
/// a PipelineAction directly — only the rule-based engine does.
/// </summary>
public abstract record PipelineAction(string AnomalyFingerprint);

public sealed record CreateAlertAction(
    string AnomalyFingerprint,
    DetectedAnomaly Source) : PipelineAction(AnomalyFingerprint);

public sealed record UpdateAlertAction(
    string AnomalyFingerprint,
    Guid ExistingAlertId,
    DetectedAnomaly Source) : PipelineAction(AnomalyFingerprint);

public sealed record CreateOptimizationAction(
    string AnomalyFingerprint,
    ProposedOptimization Source) : PipelineAction(AnomalyFingerprint);

public sealed record UpdateTowerAction(
    string TowerCode,
    TowerStatusChange? StatusChange,
    TowerMetricUpdate? MetricUpdate) : PipelineAction(AnomalyFingerprint: string.Empty);
