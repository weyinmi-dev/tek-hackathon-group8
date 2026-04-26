using Modules.Analytics.Domain;

namespace Modules.Analytics.Infrastructure.Database;

internal sealed class UnitOfWork(AnalyticsDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
