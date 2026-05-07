using Microsoft.EntityFrameworkCore;
using Modules.Analytics.Domain.Ingestion;
using Modules.Analytics.Infrastructure.Database;

namespace Modules.Analytics.Infrastructure.Repositories;

internal sealed class IngestionDashboardRepository(AnalyticsDbContext db) : IIngestionDashboardRepository
{
    public async Task AddAsync(IngestionDashboardEntry entry, CancellationToken cancellationToken = default) =>
        await db.IngestionDashboardEntries.AddAsync(entry, cancellationToken);

    public Task<bool> ExistsForRunAsync(Guid ingestionRunId, CancellationToken cancellationToken = default) =>
        db.IngestionDashboardEntries
            .AsNoTracking()
            .AnyAsync(e => e.IngestionRunId == ingestionRunId, cancellationToken);

    public async Task<IReadOnlyList<IngestionDashboardEntry>> ListRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        await db.IngestionDashboardEntries
            .AsNoTracking()
            .OrderByDescending(e => e.CompletedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
