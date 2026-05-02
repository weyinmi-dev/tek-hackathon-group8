using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Events;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Repositories;

internal sealed class EnergyPredictionRepository(EnergyDbContext db) : IEnergyPredictionRepository
{
    public async Task AddAsync(EnergyPrediction p, CancellationToken ct = default) =>
        await db.Predictions.AddAsync(p, ct);

    public async Task<IReadOnlyList<EnergyPrediction>> ListActiveAsync(CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        return await db.Predictions.AsNoTracking()
            .Where(p => p.WindowEndsUtc >= now)
            .OrderBy(p => p.WindowEndsUtc)
            .ToListAsync(ct);
    }
}
