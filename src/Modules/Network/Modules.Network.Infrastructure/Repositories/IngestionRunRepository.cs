using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Repositories;

internal sealed class IngestionRunRepository(NetworkDbContext db) : IIngestionRunRepository
{
    public Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.IngestionRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<IngestionRun?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default) =>
        db.IngestionRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ContentHash == contentHash, cancellationToken);

    public async Task AddAsync(IngestionRun run, CancellationToken cancellationToken = default) =>
        await db.IngestionRuns.AddAsync(run, cancellationToken);

    public async Task AddEventsAsync(IEnumerable<NetworkEvent> events, CancellationToken cancellationToken = default) =>
        await db.NetworkEvents.AddRangeAsync(events, cancellationToken);

    public async Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid ingestionRunId, CancellationToken cancellationToken = default) =>
        await db.NetworkEvents
            .AsNoTracking()
            .Where(e => e.IngestionRunId == ingestionRunId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
}
