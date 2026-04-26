using Modules.Ai.Domain;

namespace Modules.Ai.Infrastructure.Database;

internal sealed class UnitOfWork(AiDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
