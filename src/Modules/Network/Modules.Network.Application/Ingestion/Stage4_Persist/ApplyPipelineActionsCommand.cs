using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Domain.Ingestion;

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

    // Energy anomalies derived by rule from the reported plant state. Created when a condition first
    // appears, updated while it persists, archived (auto-acknowledged) when it clears.
    int AnomaliesCreated = 0,
    int AnomaliesUpdated = 0,
    int AnomaliesResolved = 0,

    IReadOnlyList<string>? Warnings = null,
    IReadOnlyList<SyncChange>? Changes = null)
{
    /// <summary>The itemised record of what this run touched — what the sync report's table renders.</summary>
    public IReadOnlyList<SyncChange> Changes { get; init; } = Changes ?? [];

    /// <summary>
    /// Non-fatal problems worth surfacing: a snapshot referencing a tower we skipped, an alarm with
    /// no id. These do not fail the run — a feed is allowed to be imperfect — but they must not be
    /// swallowed either, or a partially-applied sync would look like a clean one.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Warnings ?? [];

    /// <summary>
    /// The headline totals, derived from the itemised change list rather than summed from the
    /// per-aggregate counters.
    ///
    /// This is a correctness fix, not a tidy-up. Summing the counters let the tiles disagree with the
    /// table beneath them: a snapshot upload flattens into a reading that ALSO runs through the
    /// analyzer, so one tower could be touched by both the snapshot path and the AI path and get
    /// counted twice — while the report showed a single row for it. "3 updated" over a table listing
    /// two is the kind of arithmetic that destroys an operator's trust in everything else on the page.
    ///
    /// Deriving from the rows makes the two structurally incapable of diverging, and de-duplicating by
    /// (type, key) means one record touched twice by one upload is one record changed. Optimizations
    /// are deliberately absent from both — they are proposals, not synchronised records, and have
    /// their own tile.
    /// </summary>
    public int TotalCreated => RecordedChanges.Count(c => c.Action == SyncAction.Created);

    public int TotalUpdated => RecordedChanges.Count(c => c.Action == SyncAction.Updated);

    public int TotalArchived => RecordedChanges.Count(c => c.Action == SyncAction.Archived);

    /// <summary>
    /// The change list as it is persisted and rendered: one entry per record this run touched.
    ///
    /// This — not <see cref="Changes"/> — is what the run stores, so the table an operator reads and
    /// the totals above it are literally the same rows. They cannot drift apart, because there is
    /// only one list.
    ///
    /// Where a record was touched more than once in a run, the most significant action wins: a tower
    /// the snapshot CREATED and the analyzer then updated was created, not updated. Archived outranks
    /// updated for the same reason — a record that ended the run retired did not merely change.
    /// </summary>
    public IReadOnlyList<SyncChange> RecordedChanges =>
        [.. Changes
            .GroupBy(c => (c.EntityType, c.EntityKey), TupleComparer)
            .Select(g => g.OrderBy(c => Rank(c.Action)).First())];

    private static int Rank(SyncAction action) => action switch
    {
        SyncAction.Created => 0,
        SyncAction.Archived => 1,
        _ => 2
    };

    private static readonly IEqualityComparer<(string, string)> TupleComparer =
        new EntityKeyComparer();

    private sealed class EntityKeyComparer : IEqualityComparer<(string Type, string Key)>
    {
        public bool Equals((string Type, string Key) a, (string Type, string Key) b) =>
            string.Equals(a.Type, b.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Type, string Key) x) =>
            HashCode.Combine(
                x.Type.ToUpperInvariant(),
                x.Key.ToUpperInvariant());
    }
}
