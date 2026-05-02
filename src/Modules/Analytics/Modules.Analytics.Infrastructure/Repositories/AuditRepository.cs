using Microsoft.EntityFrameworkCore;
using Modules.Analytics.Domain.Audit;
using Modules.Analytics.Infrastructure.Database;

namespace Modules.Analytics.Infrastructure.Repositories;

internal sealed class AuditRepository(AnalyticsDbContext db) : IAuditRepository
{
    public async Task AddAsync(AuditEntry entry, CancellationToken ct = default) =>
        await db.AuditEntries.AddAsync(entry, ct);

    public async Task<IReadOnlyList<AuditEntry>> ListRecentAsync(int take, CancellationToken ct = default) =>
        await db.AuditEntries.AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.AuditEntries.CountAsync(ct);

    public async Task AddRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken ct = default) =>
        await db.AuditEntries.AddRangeAsync(entries, ct);

    public async Task<IReadOnlyList<AuditEntry>> ListByActionSinceAsync(string action, DateTime sinceUtc, int take, CancellationToken ct = default) =>
        await db.AuditEntries.AsNoTracking()
            .Where(a => a.Action == action && a.OccurredAtUtc >= sinceUtc)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(ct);
}
