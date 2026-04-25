using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Towers;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Repositories;

internal sealed class TowerRepository(NetworkDbContext db) : ITowerRepository
{
    public async Task<IReadOnlyList<Tower>> ListAsync(CancellationToken ct = default) =>
        await db.Towers.AsNoTracking().OrderBy(t => t.Region).ThenBy(t => t.Code).ToListAsync(ct);

    public Task<Tower?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        db.Towers.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code, ct);

    public async Task<IReadOnlyList<Tower>> ListByRegionAsync(string region, CancellationToken ct = default) =>
        await db.Towers.AsNoTracking().Where(t => t.Region == region).ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<Tower> towers, CancellationToken ct = default) =>
        await db.Towers.AddRangeAsync(towers, ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.Towers.CountAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
