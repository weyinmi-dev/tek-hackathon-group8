using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Maintenance;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Repositories;

internal sealed class MaintenanceTicketRepository(NetworkDbContext db) : IMaintenanceTicketRepository
{
    // Tracked: the synchronisation stage mutates what it reads (observe / complete / archive).
    public async Task<IReadOnlyList<MaintenanceTicket>> ListForSiteAsync(string siteCode, CancellationToken ct = default) =>
        await db.MaintenanceTickets
            .Where(t => t.SiteCode == siteCode.Trim().ToUpperInvariant())
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MaintenanceTicket>> ListOpenAsync(int take, CancellationToken ct = default) =>
        await db.MaintenanceTickets
            .AsNoTracking()
            .Where(t => t.Status == MaintenanceTicketStatus.Open)
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    public async Task AddAsync(MaintenanceTicket ticket, CancellationToken ct = default) =>
        await db.MaintenanceTickets.AddAsync(ticket, ct);

    public Task<int> CountAsync(MaintenanceTicketStatus? status, CancellationToken ct = default) =>
        status is null
            ? db.MaintenanceTickets.CountAsync(ct)
            : db.MaintenanceTickets.CountAsync(t => t.Status == status, ct);
}

internal sealed class EngineerRepository(NetworkDbContext db) : IEngineerRepository
{
    public Task<Engineer?> GetByEngineerIdAsync(string engineerId, CancellationToken ct = default) =>
        db.Engineers.FirstOrDefaultAsync(e => e.EngineerId == engineerId.Trim(), ct);

    public async Task<IReadOnlyList<Engineer>> ListAsync(CancellationToken ct = default) =>
        await db.Engineers
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Engineer engineer, CancellationToken ct = default) =>
        await db.Engineers.AddAsync(engineer, ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        db.Engineers.CountAsync(e => e.IsActive, ct);
}
