using Modules.Network.Domain.Ingestion;

namespace Modules.Network.UnitTests.Ingestion;

/// <summary>
/// In-memory implementation of the snapshot half of <see cref="IIngestionRunRepository"/>.
///
/// Snapshots ride the same repository as events (they are owned by the run), so every fake repo
/// in the test suite has to satisfy those members whether or not the test under it cares about
/// snapshots. Inheriting this base keeps that boilerplate in one place — a fake only overrides
/// what its own test actually exercises.
/// </summary>
internal abstract class FakeSnapshotStore
{
    public List<SiteSnapshotRecord> Snapshots { get; } = [];

    /// <summary>
    /// Run search backs the sync-history view, not the pipeline. No stage-handler test exercises it,
    /// so the base returns empty rather than reimplementing the cross-table filter; a test that cares
    /// about history should use the real repository against the in-memory context.
    /// </summary>
    /// <summary>
    /// Only the retry path deletes a run, and only the orchestrator tests exercise it — those
    /// override this. Everything else gets a no-op rather than a fake store it never touches.
    /// </summary>
    public virtual Task DeleteAsync(IngestionRun run, CancellationToken _ = default) => Task.CompletedTask;

    public Task<IReadOnlyList<IngestionRun>> SearchRunsAsync(
        string? siteCode, string? provider, string? search, int skip, int take, CancellationToken _ = default) =>
        Task.FromResult<IReadOnlyList<IngestionRun>>([]);

    public Task<int> CountRunsAsync(
        string? siteCode, string? provider, string? search, CancellationToken _ = default) =>
        Task.FromResult(0);

    public Task AddSnapshotsAsync(IEnumerable<SiteSnapshotRecord> snapshots, CancellationToken _ = default)
    {
        Snapshots.AddRange(snapshots);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SiteSnapshotRecord>> ListSnapshotsAsync(Guid ingestionRunId, CancellationToken _ = default) =>
        Task.FromResult<IReadOnlyList<SiteSnapshotRecord>>(
            Snapshots.Where(s => s.IngestionRunId == ingestionRunId).ToList());

    public Task<SiteSnapshotRecord?> GetLatestSnapshotForSiteAsync(string siteCode, CancellationToken _ = default) =>
        Task.FromResult(ForSite(siteCode).MaxBy(SortKey));

    public Task<SiteSnapshotRecord?> GetPreviousSnapshotForSiteAsync(
        string siteCode, DateTimeOffset beforeCapturedAt, CancellationToken _ = default) =>
        Task.FromResult(ForSite(siteCode).Where(s => SortKey(s) < beforeCapturedAt).MaxBy(SortKey));

    public Task<IReadOnlyList<SiteSnapshotRecord>> ListSnapshotsForSiteAsync(
        string siteCode, DateTimeOffset sinceUtc, int max, CancellationToken _ = default) =>
        Task.FromResult<IReadOnlyList<SiteSnapshotRecord>>(
            ForSite(siteCode)
                .Where(s => SortKey(s) >= sinceUtc)
                .OrderByDescending(SortKey)
                .Take(max)
                .OrderBy(SortKey)
                .ToList());

    public Task<IReadOnlyList<SiteSnapshotRecord>> SearchSnapshotsAsync(
        string? siteCode, string? provider, string? search, int skip, int take, CancellationToken _ = default) =>
        Task.FromResult<IReadOnlyList<SiteSnapshotRecord>>(
            Filter(siteCode, provider, search)
                .OrderByDescending(s => s.GeneratedAt)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountSnapshotsAsync(
        string? siteCode, string? provider, string? search, CancellationToken _ = default) =>
        Task.FromResult(Filter(siteCode, provider, search).Count());

    private IEnumerable<SiteSnapshotRecord> Filter(string? siteCode, string? provider, string? search)
    {
        IEnumerable<SiteSnapshotRecord> query = Snapshots;

        if (!string.IsNullOrWhiteSpace(siteCode))
        {
            query = query.Where(s => s.SiteCode == siteCode.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(s => s.Provider == provider);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s =>
                s.SiteCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.SiteName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Region.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Provider.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.RequestId.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private IEnumerable<SiteSnapshotRecord> ForSite(string siteCode) =>
        Snapshots.Where(s => s.SiteCode == siteCode.Trim().ToUpperInvariant());

    /// <summary>Mirrors the production ordering: measurement time, falling back to document time.</summary>
    private static DateTimeOffset SortKey(SiteSnapshotRecord s) => s.CapturedAt ?? s.GeneratedAt;
}
