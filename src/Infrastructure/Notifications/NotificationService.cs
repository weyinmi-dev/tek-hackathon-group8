using Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Notifications;

internal sealed class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
    public Task SendAsync(
        Guid userId,
        string message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Notification sent to user {UserId}: {Message}", userId, message);
        return Task.CompletedTask;
    }
}
