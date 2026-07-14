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

/// <summary>
/// What Stage 4 actually did, per aggregate. Doubles as the synchronisation report the upload UI
/// renders and the sync-history page stores — "14 created, 3 updated, 1 archived" is this record.
///
/// The counts are split created / updated / archived rather than collapsed into a single "changed"
/// because the distinction is the evidence of idempotency: a second upload of the same document
/// must show zeroes across the board, and a reader can only see that if the categories are separate.
/// </summary>
public sealed record PipelineActionCounts(
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    int TowerUpdates,

    // ── Snapshot synchronisation ────────────────────────────────────────────────
    int TowersCreated = 0,
    int AlertsResolved = 0,
    int SitesCreated = 0,
    int SitesUpdated = 0,
    int TelemetryRowsAppended = 0,
    int EquipmentCreated = 0,
    int EquipmentUpdated = 0,
    int EquipmentRetired = 0,
    int TicketsCreated = 0,
    int TicketsUpdated = 0,
    int TicketsCompleted = 0,
    int TicketsArchived = 0,
    int EngineersCreated = 0,
    int EngineersUpdated = 0,
    IReadOnlyList<string>? Warnings = null)
{
    /// <summary>
    /// Non-fatal problems worth surfacing: a snapshot referencing a tower we skipped, an alarm with
    /// no id. These do not fail the run — a feed is allowed to be imperfect — but they must not be
    /// swallowed either, or a partially-applied sync would look like a clean one.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Warnings ?? [];

    public int TotalCreated => TowersCreated + SitesCreated + EquipmentCreated + TicketsCreated + EngineersCreated + AlertsCreated;

    public int TotalUpdated => TowerUpdates + SitesUpdated + EquipmentUpdated + TicketsUpdated + EngineersUpdated + AlertsUpdated;

    public int TotalArchived => EquipmentRetired + TicketsArchived + AlertsResolved;
}
