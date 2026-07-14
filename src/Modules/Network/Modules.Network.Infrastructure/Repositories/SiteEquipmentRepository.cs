using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Assets;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Repositories;

internal sealed class SiteEquipmentRepository(NetworkDbContext db) : ISiteEquipmentRepository
{
    /// <summary>
    /// Tracked, not AsNoTracking: the synchronisation stage mutates what it reads (observe / retire)
    /// and relies on the unit of work to commit those edits alongside everything else in the run.
    /// </summary>
    public async Task<IReadOnlyList<SiteEquipment>> ListForSiteAsync(string siteCode, CancellationToken ct = default) =>
        await db.SiteEquipment
            .Where(e => e.SiteCode == siteCode.Trim().ToUpperInvariant())
            .OrderBy(e => e.EquipmentId)
            .ToListAsync(ct);

    public async Task AddAsync(SiteEquipment equipment, CancellationToken ct = default) =>
        await db.SiteEquipment.AddAsync(equipment, ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        db.SiteEquipment.CountAsync(e => e.IsActive, ct);
}
