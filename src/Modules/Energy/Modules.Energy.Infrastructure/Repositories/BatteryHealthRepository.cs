using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Repositories;

internal sealed class BatteryHealthRepository(EnergyDbContext db) : IBatteryHealthRepository
{
    public Task<BatteryHealth?> GetForSiteAsync(string siteCode, CancellationToken ct = default) =>
        db.Batteries.FirstOrDefaultAsync(b => b.SiteCode == siteCode, ct);

    public async Task<IReadOnlyList<BatteryHealth>> ListAsync(CancellationToken ct = default) =>
        await db.Batteries.AsNoTracking().ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<BatteryHealth> rows, CancellationToken ct = default) =>
        await db.Batteries.AddRangeAsync(rows, ct);
}
