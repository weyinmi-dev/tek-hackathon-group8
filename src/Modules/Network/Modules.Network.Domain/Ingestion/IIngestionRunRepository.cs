namespace Modules.Network.Domain.Ingestion;

public interface IIngestionRunRepository
{
    Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whole-file idempotency lookup. Returning a non-null run means the same
    /// content has already been ingested and the orchestrator should short-circuit
    /// without dispatching the stages.
    /// </summary>
    Task<IngestionRun?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);

    Task AddAsync(IngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a run and, by cascade, its events and snapshots.
    ///
    /// Used only to clear a FAILED run before retrying the same file. ContentHash is uniquely
    /// indexed — one run per file content — so a retry cannot simply insert a second row alongside
    /// the failure; it has to replace it. Without this, re-uploading a file that failed once fails
    /// forever with a duplicate-key error, which is not a retry story anyone would choose.
    /// </summary>
    Task DeleteAsync(IngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// The synchronisation history, newest first. Filters narrow by the snapshot(s) a run carried,
    /// so "show me every upload for LAG0456" works even though the site code lives on the snapshot
    /// rather than on the run.
    /// </summary>
    Task<IReadOnlyList<IngestionRun>> SearchRunsAsync(
        string? siteCode, string? provider, string? search, int skip, int take,
        CancellationToken cancellationToken = default);

    Task<int> CountRunsAsync(
        string? siteCode, string? provider, string? search, CancellationToken cancellationToken = default);

    Task AddEventsAsync(IEnumerable<NetworkEvent> events, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid ingestionRunId, CancellationToken cancellationToken = default);

    // ── Site snapshots ────────────────────────────────────────────────────────
    // Snapshots ride the same run as events, so they use the same repository. Stage 1
    // writes them; Stage 3 reads them back to plan the synchronisation; the query side
    // reads them for site details, telemetry trends, and the file index.

    Task AddSnapshotsAsync(IEnumerable<SiteSnapshotRecord> snapshots, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SiteSnapshotRecord>> ListSnapshotsAsync(Guid ingestionRunId, CancellationToken cancellationToken = default);

    /// <summary>Most recent snapshot for a site — the "current reported state" a site-details view renders.</summary>
    Task<SiteSnapshotRecord?> GetLatestSnapshotForSiteAsync(string siteCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// The snapshot immediately preceding <paramref name="beforeCapturedAt"/> for a site. Backs
    /// "what changed since the last upload" comparisons.
    /// </summary>
    Task<SiteSnapshotRecord?> GetPreviousSnapshotForSiteAsync(
        string siteCode, DateTimeOffset beforeCapturedAt, CancellationToken cancellationToken = default);

    /// <summary>Ordered snapshot history for a site — the source for every telemetry trend chart.</summary>
    Task<IReadOnlyList<SiteSnapshotRecord>> ListSnapshotsForSiteAsync(
        string siteCode, DateTimeOffset sinceUtc, int max, CancellationToken cancellationToken = default);

    /// <summary>Newest-first snapshot index, optionally narrowed by site, provider, or free-text search.</summary>
    Task<IReadOnlyList<SiteSnapshotRecord>> SearchSnapshotsAsync(
        string? siteCode, string? provider, string? search, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountSnapshotsAsync(
        string? siteCode, string? provider, string? search, CancellationToken cancellationToken = default);
}
