using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Optimizations;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Repositories;

internal sealed class OptimizationRepository(NetworkDbContext db) : IOptimizationRepository
{
    public async Task AddAsync(Optimization optimization, CancellationToken cancellationToken = default) =>
        await db.Optimizations.AddAsync(optimization, cancellationToken);

    public async Task<IReadOnlyList<Optimization>> ListByRunAsync(Guid ingestionRunId, CancellationToken cancellationToken = default) =>
        await db.Optimizations
            .AsNoTracking()
            .Where(o => o.IngestionRunId == ingestionRunId)
            .OrderBy(o => o.ProposedAt)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        db.Optimizations.CountAsync(cancellationToken);
}
