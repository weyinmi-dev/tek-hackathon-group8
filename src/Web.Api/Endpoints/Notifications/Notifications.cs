using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Analytics.Application.Notifications;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notifications;

/// <summary>
/// The operator notification feed: critical alarms, new uploads, synchronisation failures and
/// partial syncs. Raised by the Stage-5 subscribers in Analytics; polled by the frontend on the same
/// cadence as everything else, since the app has no push transport.
/// </summary>
public sealed class Notifications : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("notifications", [Authorize]
            async (bool? unreadOnly, int? take, ISender sender, CancellationToken ct) =>
        {
            Result<NotificationFeed> result = await sender.Send(
                new ListNotificationsQuery(unreadOnly ?? false, take ?? 30), ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Notifications);

        app.MapPost("notifications/{id:guid}/read", [Authorize]
            async (Guid id, ISender sender, CancellationToken ct) =>
        {
            Result result = await sender.Send(new MarkNotificationReadCommand(id), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Notifications);

        app.MapPost("notifications/read-all", [Authorize]
            async (ISender sender, CancellationToken ct) =>
        {
            Result<int> result = await sender.Send(new MarkAllNotificationsReadCommand(), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Notifications);
    }
}
