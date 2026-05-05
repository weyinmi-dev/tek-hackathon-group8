using SharedKernel;

namespace Modules.Network.Domain.Optimizations;

/// <summary>
/// Pipeline-proposed network-improvement action targeting a specific tower. One row per
/// emission — append-only by design. The orchestrator's content-hash idempotency check
/// stops re-uploads from creating duplicates; within a single ingestion run the decision
/// engine deduplicates by (tower, type) before emitting CreateOptimizationAction.
/// </summary>
public sealed class Optimization : Entity
{
    private Optimization(
        Guid id,
        Guid ingestionRunId,
        string towerCode,
        string anomalyFingerprint,
        OptimizationType type,
        decimal estimatedImpact,
        string rationale,
        DateTimeOffset proposedAt) : base(id)
    {
        IngestionRunId = ingestionRunId;
        TowerCode = towerCode;
        AnomalyFingerprint = anomalyFingerprint;
        Type = type;
        EstimatedImpact = estimatedImpact;
        Rationale = rationale;
        ProposedAt = proposedAt;
    }

    private Optimization() { }

    public Guid IngestionRunId { get; private set; }
    public string TowerCode { get; private set; } = null!;

    /// <summary>
    /// Empty when the optimization wasn't correlated to an anomaly in the same batch
    /// (orphan recommendation). Useful for joining recommendations to their motivating alert.
    /// </summary>
    public string AnomalyFingerprint { get; private set; } = null!;

    public OptimizationType Type { get; private set; }
    public decimal EstimatedImpact { get; private set; }
    public string Rationale { get; private set; } = null!;
    public DateTimeOffset ProposedAt { get; private set; }

    public static Optimization Propose(
        Guid ingestionRunId,
        string towerCode,
        string anomalyFingerprint,
        OptimizationType type,
        decimal estimatedImpact,
        string rationale,
        DateTimeOffset proposedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(towerCode);
        ArgumentNullException.ThrowIfNull(anomalyFingerprint);  // empty allowed (orphan)
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);

        if (estimatedImpact is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedImpact), estimatedImpact, "Estimated impact must be in [0, 1].");
        }

        return new Optimization(
            Guid.NewGuid(),
            ingestionRunId,
            towerCode.Trim().ToUpperInvariant(),
            anomalyFingerprint,
            type,
            estimatedImpact,
            rationale,
            proposedAt);
    }
}

/// <summary>
/// Closed set the AI is allowed to propose. Mirrors the wire-level enum in
/// <c>Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts.OptimizationType</c>;
/// kept duplicated here so Domain stays free of Application dependencies.
/// </summary>
public enum OptimizationType
{
    LoadBalance = 0,
    PowerAdjust = 1,
    RouteReconfigure = 2,
    AntennaRetune = 3,
    CapacityExpansion = 4
}

public interface IOptimizationRepository
{
    Task AddAsync(Optimization optimization, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Optimization>> ListByRunAsync(Guid ingestionRunId, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
