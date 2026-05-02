using Modules.Energy.Domain;

namespace Modules.Energy.Infrastructure.Database;

internal sealed class UnitOfWork(EnergyDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
