using Application.Abstractions.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Analytics.Domain;
using Modules.Analytics.Domain.Notifications;
using Modules.Network.Application.Ingestion.Pipeline;
using SharedKernel;

namespace Modules.Analytics.Application.Notifications;

/// <summary>
/// Turns pipeline outcomes into the operator notification feed. Subscribes to the same Stage-5
/// integration events the dashboard projection already listens to — no new bus, no new job, no
/// polling.
///
/// It is deliberately conservative about what it raises. A notification that fires on every upload
/// is a notification nobody reads, so this raises one only when something actually happened: records
/// changed, alerts were raised, or the run failed. A clean no-op re-upload produces nothing.
/// </summary>
internal sealed class PipelineCompletedNotificationFeedHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ILogger<PipelineCompletedNotificationFeedHandler> logger)
    : INotificationHandler<PipelineCompletedNotification>
{
    public async Task Handle(PipelineCompletedNotification e, CancellationToken cancellationToken)
    {
        var raised = new List<Notification>();

        string sites = e.SiteCodes.Count > 0 ? string.Join(", ", e.SiteCodes) : "network log";
        string? primarySite = e.SiteCodes.Count == 1 ? e.SiteCodes[0] : null;

        // ── New upload ──────────────────────────────────────────────────────────
        // Only when it changed something. A deduplicated re-upload changes nothing and is not news.
        bool changedSomething = e.RecordsCreated + e.RecordsUpdated + e.RecordsArchived > 0;
        if (changedSomething)
        {
            raised.Add(Notification.Raise(
                NotificationKind.UploadCompleted,
                NotificationSeverity.Info,
                title: $"Synchronised {sites}",
                body: $"{e.FileName}: {e.RecordsCreated} created, {e.RecordsUpdated} updated, " +
                      $"{e.RecordsArchived} archived.",
                siteCode: primarySite,
                link: $"/sync/{e.IngestionRunId}",

                // One per run. The run id is already unique, so this only guards a redelivery of the
                // same event — which the in-memory bus can produce on retry.
                dedupeKey: $"upload:{e.IngestionRunId}"));
        }

        // ── Critical alarms ─────────────────────────────────────────────────────
        if (e.CriticalAlertsRaised > 0)
        {
            raised.Add(Notification.Raise(
                NotificationKind.CriticalAlarm,
                NotificationSeverity.Critical,
                title: $"{e.CriticalAlertsRaised} new alert(s) on {sites}",
                body: "New alerts were raised by the latest synchronisation. Review and dispatch.",
                siteCode: primarySite,
                link: "/alerts",
                dedupeKey: $"alerts:{e.IngestionRunId}"));
        }

        // ── Warnings ────────────────────────────────────────────────────────────
        // A run that succeeded with warnings applied only part of what the feed sent. Saying nothing
        // would present a partial sync as a clean one.
        if (e.WarningCount > 0)
        {
            raised.Add(Notification.Raise(
                NotificationKind.HealthDegraded,
                NotificationSeverity.Warn,
                title: $"Synchronisation of {sites} completed with {e.WarningCount} warning(s)",
                body: "Some of the reported data could not be applied. Open the sync report for details.",
                siteCode: primarySite,
                link: $"/sync/{e.IngestionRunId}",
                dedupeKey: $"warnings:{e.IngestionRunId}"));
        }

        await PersistAsync(raised, notifications, unitOfWork, logger, cancellationToken);
    }

    internal static async Task PersistAsync(
        IReadOnlyList<Notification> raised,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        int persisted = 0;

        foreach (Notification notification in raised)
        {
            if (notification.DedupeKey is { } key &&
                await notifications.ExistsUnreadAsync(key, cancellationToken))
            {
                continue;
            }

            await notifications.AddAsync(notification, cancellationToken);
            persisted++;
        }

        if (persisted > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Raised {Count} operator notification(s)", persisted);
        }
    }
}

/// <summary>
/// A run that failed is the one an operator most needs to hear about — it means the feed has stopped
/// landing and every view is quietly going stale.
/// </summary>
internal sealed class PipelineFailedNotificationFeedHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ILogger<PipelineFailedNotificationFeedHandler> logger)
    : INotificationHandler<PipelineFailedNotification>
{
    public async Task Handle(PipelineFailedNotification e, CancellationToken cancellationToken)
    {
        Notification notification = Notification.Raise(
            NotificationKind.SynchronizationFailed,
            NotificationSeverity.Critical,
            title: $"Synchronisation failed — {e.FileName}",
            body: e.Reason,
            siteCode: null,
            link: $"/sync/{e.IngestionRunId}",
            dedupeKey: $"failed:{e.IngestionRunId}");

        await PipelineCompletedNotificationFeedHandler.PersistAsync(
            [notification], notifications, unitOfWork, logger, cancellationToken);
    }
}

// ── Query / command side ─────────────────────────────────────────────────────

public sealed record ListNotificationsQuery(bool UnreadOnly = false, int Take = 30)
    : IQuery<NotificationFeed>;

public sealed record NotificationFeed(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public sealed record NotificationDto(
    Guid Id,
    string Kind,
    string Severity,
    string Title,
    string Body,
    string? SiteCode,
    string? Link,
    DateTime RaisedAtUtc,
    bool IsRead);

internal sealed class ListNotificationsQueryHandler(INotificationRepository notifications)
    : IQueryHandler<ListNotificationsQuery, NotificationFeed>
{
    public async Task<Result<NotificationFeed>> Handle(
        ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Notification> rows = await notifications.ListAsync(
            request.UnreadOnly, Math.Clamp(request.Take, 1, 100), cancellationToken);

        int unread = await notifications.CountUnreadAsync(cancellationToken);

        return Result.Success(new NotificationFeed(
            rows.Select(n => new NotificationDto(
                n.Id,
                n.Kind.ToString(),
                n.Severity.ToString().ToLowerInvariant(),
                n.Title,
                n.Body,
                n.SiteCode,
                n.Link,
                n.RaisedAtUtc,
                n.IsRead)).ToList(),
            unread));
    }
}

public sealed record MarkNotificationReadCommand(Guid NotificationId) : ICommand;

internal sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<MarkNotificationReadCommand>
{
    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        Notification? notification = await notifications.GetAsync(request.NotificationId, cancellationToken);
        if (notification is null)
        {
            return Result.Failure(Error.NotFound(
                "Notification.NotFound", $"Notification {request.NotificationId} not found."));
        }

        if (notification.MarkRead())
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

public sealed record MarkAllNotificationsReadCommand : ICommand<int>;

internal sealed class MarkAllNotificationsReadCommandHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<MarkAllNotificationsReadCommand, int>
{
    public async Task<Result<int>> Handle(
        MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Notification> unread = await notifications.ListUnreadAsync(cancellationToken);

        int marked = unread.Count(n => n.MarkRead());
        if (marked > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(marked);
    }
}
