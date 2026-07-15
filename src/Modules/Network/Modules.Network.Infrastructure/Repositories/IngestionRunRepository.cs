using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Repositories;

internal sealed class IngestionRunRepository(NetworkDbContext db) : IIngestionRunRepository
{
    public Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.IngestionRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<IngestionRun?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default) =>
        db.IngestionRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ContentHash == contentHash, cancellationToken);

    public async Task AddAsync(IngestionRun run, CancellationToken cancellationToken = default) =>
        await db.IngestionRuns.AddAsync(run, cancellationToken);

    public Task DeleteAsync(IngestionRun run, CancellationToken cancellationToken = default)
    {
        // The prior run may have been read AsNoTracking (GetByContentHashAsync). Attach it so EF has
        // something to delete; the FK cascades take its events and snapshots with it.
        db.IngestionRuns.Remove(db.IngestionRuns.Local.FirstOrDefault(r => r.Id == run.Id) ?? run);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<IngestionRun>> SearchRunsAsync(
        string? siteCode, string? provider, string? search, int skip, int take,
        CancellationToken cancellationToken = default) =>
        await FilterRuns(siteCode, provider, search)
            .OrderByDescending(r => r.StartedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

    public Task<int> CountRunsAsync(
        string? siteCode, string? provider, string? search, CancellationToken cancellationToken = default) =>
        FilterRuns(siteCode, provider, search).CountAsync(cancellationToken);

    /// <summary>
    /// The site code and provider live on the snapshot, not on the run, so narrowing by them means
    /// asking "does this run have a snapshot that matches?" — expressed as a correlated subquery so
    /// a run carrying several sites is returned once, not once per site.
    /// </summary>
    private IQueryable<IngestionRun> FilterRuns(string? siteCode, string? provider, string? search)
    {
        IQueryable<IngestionRun> query = db.IngestionRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(siteCode))
        {
            string code = Normalize(siteCode);
            query = query.Where(r => db.SiteSnapshots.Any(s => s.IngestionRunId == r.Id && s.SiteCode == code));
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(r => db.SiteSnapshots.Any(s => s.IngestionRunId == r.Id && s.Provider == provider));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = $"%{search.Trim()}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.FileName, term) ||
                EF.Functions.ILike(r.SubmittedBy, term) ||
                db.SiteSnapshots.Any(s => s.IngestionRunId == r.Id &&
                    (EF.Functions.ILike(s.SiteCode, term) ||
                     EF.Functions.ILike(s.SiteName, term) ||
                     EF.Functions.ILike(s.Provider, term) ||
                     EF.Functions.ILike(s.Region, term))));
        }

        return query;
    }

    public async Task AddEventsAsync(IEnumerable<NetworkEvent> events, CancellationToken cancellationToken = default) =>
        await db.NetworkEvents.AddRangeAsync(events, cancellationToken);

    public async Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid ingestionRunId, CancellationToken cancellationToken = default) =>
        await db.NetworkEvents
            .AsNoTracking()
            .Where(e => e.IngestionRunId == ingestionRunId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

    // ── Site snapshots ────────────────────────────────────────────────────────

    public async Task AddSnapshotsAsync(
        IEnumerable<SiteSnapshotRecord> snapshots, CancellationToken cancellationToken = default) =>
        await db.SiteSnapshots.AddRangeAsync(snapshots, cancellationToken);

    public async Task<IReadOnlyList<SiteSnapshotRecord>> ListSnapshotsAsync(
        Guid ingestionRunId, CancellationToken cancellationToken = default) =>
        await db.SiteSnapshots
            .AsNoTracking()
            .Where(s => s.IngestionRunId == ingestionRunId)
            .OrderBy(s => s.SiteCode)
            .ToListAsync(cancellationToken);

    public Task<SiteSnapshotRecord?> GetLatestSnapshotForSiteAsync(
        string siteCode, CancellationToken cancellationToken = default)
    {
        string code = Normalize(siteCode);

        // Order by the measurement time, falling back to the document time for snapshots that
        // carried no performance block — otherwise those would always sort last and a site whose
        // only snapshots lack metrics would look like it had never reported.
        return db.SiteSnapshots
            .AsNoTracking()
            .Where(s => s.SiteCode == code)
            .OrderByDescending(s => s.CapturedAt ?? s.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SiteSnapshotRecord?> GetPreviousSnapshotForSiteAsync(
        string siteCode, DateTimeOffset beforeCapturedAt, CancellationToken cancellationToken = default)
    {
        string code = Normalize(siteCode);

        return db.SiteSnapshots
            .AsNoTracking()
            .Where(s => s.SiteCode == code && (s.CapturedAt ?? s.GeneratedAt) < beforeCapturedAt)
            .OrderByDescending(s => s.CapturedAt ?? s.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SiteSnapshotRecord>> ListSnapshotsForSiteAsync(
        string siteCode, DateTimeOffset sinceUtc, int max, CancellationToken cancellationToken = default)
    {
        string code = Normalize(siteCode);

        // Take the newest `max` inside the window, then flip back to chronological order so the
        // caller can plot straight from the list. Ordering ascending and taking the first `max`
        // would silently truncate a long window to its oldest points.
        List<SiteSnapshotRecord> rows = await db.SiteSnapshots
            .AsNoTracking()
            .Where(s => s.SiteCode == code && (s.CapturedAt ?? s.GeneratedAt) >= sinceUtc)
            .OrderByDescending(s => s.CapturedAt ?? s.GeneratedAt)
            .Take(Math.Clamp(max, 1, 5000))
            .ToListAsync(cancellationToken);

        rows.Reverse();
        return rows;
    }

    public async Task<IReadOnlyList<SiteSnapshotRecord>> SearchSnapshotsAsync(
        string? siteCode, string? provider, string? search, int skip, int take,
        CancellationToken cancellationToken = default) =>
        await FilterSnapshots(siteCode, provider, search)
            .OrderByDescending(s => s.GeneratedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

    public Task<int> CountSnapshotsAsync(
        string? siteCode, string? provider, string? search, CancellationToken cancellationToken = default) =>
        FilterSnapshots(siteCode, provider, search).CountAsync(cancellationToken);

    private IQueryable<SiteSnapshotRecord> FilterSnapshots(string? siteCode, string? provider, string? search)
    {
        IQueryable<SiteSnapshotRecord> query = db.SiteSnapshots.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(siteCode))
        {
            string code = Normalize(siteCode);
            query = query.Where(s => s.SiteCode == code);
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(s => s.Provider == provider);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.SiteCode, term) ||
                EF.Functions.ILike(s.SiteName, term) ||
                EF.Functions.ILike(s.Region, term) ||
                EF.Functions.ILike(s.Provider, term) ||
                EF.Functions.ILike(s.RequestId, term));
        }

        return query;
    }

    private static string Normalize(string siteCode) => siteCode.Trim().ToUpperInvariant();
}
