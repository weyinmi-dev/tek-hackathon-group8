using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Repositories;

internal sealed class SiteRepository(EnergyDbContext db) : ISiteRepository
{
    // Reads use AsNoTracking — site queries are read-heavy (dashboards, MCP) and we
    // never mutate the returned entities. Mutations go through GetByCode/GetById.
    public async Task<IReadOnlyList<Site>> ListAsync(CancellationToken ct = default) =>
        await db.Sites.AsNoTracking().OrderBy(s => s.Region).ThenBy(s => s.Code).ToListAsync(ct);

    public Task<Site?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        db.Sites.FirstOrDefaultAsync(s => s.Code == code, ct);

    public Task<Site?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Sites.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddRangeAsync(IEnumerable<Site> sites, CancellationToken ct = default) =>
        await db.Sites.AddRangeAsync(sites, ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.Sites.CountAsync(ct);
}
