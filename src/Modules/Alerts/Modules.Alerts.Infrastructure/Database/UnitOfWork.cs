using Modules.Alerts.Domain;

namespace Modules.Alerts.Infrastructure.Database;

internal sealed class UnitOfWork(AlertsDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
