using Modules.Network.Domain;

namespace Modules.Network.Infrastructure.Database;

internal sealed class UnitOfWork(NetworkDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
