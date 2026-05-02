using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Events;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Repositories;

internal sealed class AnomalyEventRepository(EnergyDbContext db) : IAnomalyEventRepository
{
    public async Task AddAsync(AnomalyEvent ev, CancellationToken ct = default) =>
        await db.Anomalies.AddAsync(ev, ct);

    public Task<AnomalyEvent?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Anomalies.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<AnomalyEvent>> ListAsync(int take, CancellationToken ct = default) =>
        await db.Anomalies.AsNoTracking()
            .OrderByDescending(a => a.DetectedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AnomalyEvent>> ListOpenForSiteAsync(string siteCode, CancellationToken ct = default) =>
        await db.Anomalies.AsNoTracking()
            .Where(a => a.SiteCode == siteCode && !a.Acknowledged)
            .OrderByDescending(a => a.DetectedAtUtc)
            .ToListAsync(ct);

    public Task<int> CountAsync(AnomalySeverity? minSeverity, bool openOnly, CancellationToken ct = default)
    {
        IQueryable<AnomalyEvent> q = db.Anomalies.AsNoTracking();
        if (openOnly) q = q.Where(a => !a.Acknowledged);
        if (minSeverity is { } sev) q = q.Where(a => (int)a.Severity >= (int)sev);
        return q.CountAsync(ct);
    }

    public Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        db.Anomalies.AsNoTracking().CountAsync(a => a.DetectedAtUtc >= sinceUtc, ct);
}
