using Microsoft.EntityFrameworkCore;
using Modules.Alerts.Domain.Alerts;
using Modules.Alerts.Infrastructure.Database;

namespace Modules.Alerts.Infrastructure.Repositories;

internal sealed class AlertRepository(AlertsDbContext db) : IAlertRepository
{
    public async Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Alerts.AsNoTracking().OrderByDescending(a => a.RaisedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Alert>> ListBySeverityAsync(AlertSeverity severity, CancellationToken cancellationToken = default) =>
        await db.Alerts.AsNoTracking().Where(a => a.Severity == severity).OrderByDescending(a => a.RaisedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Alert>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await db.Alerts.AsNoTracking()
            .Where(a => a.Status == AlertStatus.Active || a.Status == AlertStatus.Investigating || a.Status == AlertStatus.Monitoring)
            .OrderByDescending(a => a.RaisedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<Alert?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.Alerts.FirstOrDefaultAsync(a => a.Code == code, cancellationToken);

    public Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Alerts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Alert> alerts, CancellationToken cancellationToken = default) =>
        await db.Alerts.AddRangeAsync(alerts, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => db.Alerts.CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<AlertSeverity, int>> CountBySeverityAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Alerts.AsNoTracking()
            .GroupBy(a => a.Severity)
            .Select(g => new { Sev = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Sev, r => r.Count);
    }
}
