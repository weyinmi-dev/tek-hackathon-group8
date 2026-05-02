using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Telemetry;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Repositories;

internal sealed class SiteEnergyLogRepository(EnergyDbContext db) : ISiteEnergyLogRepository
{
    public async Task AddRangeAsync(IEnumerable<SiteEnergyLog> rows, CancellationToken ct = default) =>
        await db.SiteLogs.AddRangeAsync(rows, ct);

    public async Task<IReadOnlyList<SiteEnergyLog>> ListForSiteAsync(string siteCode, int hours, CancellationToken ct = default)
    {
        DateTime since = DateTime.UtcNow.AddHours(-Math.Max(1, hours));
        return await db.SiteLogs.AsNoTracking()
            .Where(l => l.SiteCode == siteCode && l.RecordedAtUtc >= since)
            .OrderBy(l => l.RecordedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SiteEnergyLog>> ListSinceAsync(DateTime sinceUtc, int max, CancellationToken ct = default) =>
        await db.SiteLogs.AsNoTracking()
            .Where(l => l.RecordedAtUtc >= sinceUtc)
            .OrderByDescending(l => l.RecordedAtUtc)
            .Take(max)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.SiteLogs.CountAsync(ct);
}
