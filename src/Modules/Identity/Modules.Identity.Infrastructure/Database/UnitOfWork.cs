using Modules.Identity.Domain;

namespace Modules.Identity.Infrastructure.Database;

internal sealed class UnitOfWork(IdentityDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
