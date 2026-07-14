using Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;
using Modules.Analytics.Domain;
using Modules.Analytics.Domain.Notifications;

namespace Modules.Analytics.Infrastructure.Notifications;

/// <summary>
/// A real implementation of the pre-existing <see cref="INotificationService"/>, which until now was
/// a stub that logged the message and dropped it. It writes to the same feed the pipeline handlers
/// raise into, so a notification sent through this port and one raised by a synchronisation are the
/// same kind of thing and show up in the same place.
///
/// Registered by Analytics' DI, which runs after <c>AddInfrastructure</c> in the composition root —
/// so this replaces the log-only stub rather than sitting alongside it as a second implementation.
///
/// The <paramref name="userId"/> the interface takes is recorded in the body rather than used for
/// routing. There is no per-user delivery in this system and pretending otherwise would be a
/// fiction; the feed is NOC-wide. Changing the interface is left alone deliberately — it is a public
/// abstraction with other potential callers, and widening it is a separate decision.
/// </summary>
internal sealed class PersistentNotificationService(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ILogger<PersistentNotificationService> logger) : INotificationService
{
    public async Task SendAsync(Guid userId, string message, CancellationToken cancellationToken = default)
    {
        Notification notification = Notification.Raise(
            NotificationKind.UploadCompleted,
            NotificationSeverity.Info,
            title: message.Length <= 200 ? message : message[..197] + "…",
            body: message);

        await notifications.AddAsync(notification, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Notification raised for user {UserId}: {Message}", userId, message);
    }
}
