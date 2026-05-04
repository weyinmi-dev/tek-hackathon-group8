using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Events;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Repositories;

internal sealed class FuelEventRepository(EnergyDbContext db) : IFuelEventRepository
{
    public async Task AddAsync(FuelEvent ev, CancellationToken ct = default) =>
        await db.FuelEvents.AddAsync(ev, ct);

    public async Task<IReadOnlyList<FuelEvent>> ListForSiteAsync(string siteCode, int hours, CancellationToken ct = default)
    {
        DateTime since = DateTime.UtcNow.AddHours(-Math.Max(1, hours));
        return await db.FuelEvents.AsNoTracking()
            .Where(e => e.SiteCode == siteCode && e.OccurredAtUtc >= since)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ToListAsync(ct);
    }

    public Task<int> CountSinceAsync(DateTime sinceUtc, FuelEventKind kind, CancellationToken ct = default) =>
        db.FuelEvents.AsNoTracking().CountAsync(e => e.OccurredAtUtc >= sinceUtc && e.Kind == kind, ct);
}
