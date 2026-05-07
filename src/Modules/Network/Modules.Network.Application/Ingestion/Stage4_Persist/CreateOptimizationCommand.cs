using Application.Abstractions.Messaging;
using Modules.Network.Domain.Optimizations;

namespace Modules.Network.Application.Ingestion.Stage4_Persist;

/// <summary>
/// Stage-4 sub-command dispatched per <c>CreateOptimizationAction</c>. Persists a single
/// <see cref="Optimization"/> aggregate. Same MediatR-command pattern as the alert flow,
/// but stays inside Network — Optimization is a Network concept, no cross-module port needed.
/// </summary>
public sealed record CreateOptimizationCommand(
    Guid IngestionRunId,
    string TowerCode,
    string AnomalyFingerprint,
    OptimizationType Type,
    decimal EstimatedImpact,
    string Rationale,
    DateTimeOffset ProposedAt) : ICommand<Guid>;
