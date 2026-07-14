using Microsoft.EntityFrameworkCore;
using Modules.Analytics.Domain.Notifications;
using Modules.Analytics.Infrastructure.Database;

namespace Modules.Analytics.Infrastructure.Repositories;

internal sealed class NotificationRepository(AnalyticsDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> ListAsync(
        bool unreadOnly, int take, CancellationToken ct = default)
    {
        IQueryable<Notification> query = db.Notifications.AsNoTracking();

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.RaisedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    // Tracked: the caller is about to mark it read.
    public Task<Notification?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Notification>> ListUnreadAsync(CancellationToken ct = default) =>
        await db.Notifications.Where(n => !n.IsRead).ToListAsync(ct);

    public Task<int> CountUnreadAsync(CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => !n.IsRead, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default) =>
        await db.Notifications.AddAsync(notification, ct);

    public Task<bool> ExistsUnreadAsync(string dedupeKey, CancellationToken ct = default) =>
        db.Notifications.AnyAsync(n => n.DedupeKey == dedupeKey && !n.IsRead, ct);
}
