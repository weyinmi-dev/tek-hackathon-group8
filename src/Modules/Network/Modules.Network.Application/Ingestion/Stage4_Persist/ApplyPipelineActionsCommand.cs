using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;

namespace Modules.Network.Application.Ingestion.Stage4_Persist;

/// <summary>
/// Stage 4 — executes the side-effecting actions the decision engine produced.
/// Alert actions are dispatched cross-module via <c>IAlertActionExecutor</c>; tower
/// actions are applied locally via <c>ITowerRepository</c>. Optimization actions
/// are counted only (real persistence is a follow-up; not in success criteria).
///
/// Pre-condition: the run must be in <c>Persisting</c>; the orchestrator owns transitions.
/// </summary>
public sealed record ApplyPipelineActionsCommand(
    Guid IngestionRunId,
    IReadOnlyList<PipelineAction> Actions) : ICommand<PipelineActionCounts>, IIngestionPipelineRequest
{
    public string StageName => "Persist";
}

public sealed record PipelineActionCounts(
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    int TowerUpdates);
