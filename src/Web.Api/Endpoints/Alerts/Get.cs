using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Alerts.Application.Alerts.Acknowledge;
using Modules.Alerts.Application.Alerts.GetAlerts;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Alerts;

public sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/alerts?severity=critical&active=true
        app.MapGet("alerts", [Authorize] async (string? severity, bool? active, ISender sender, CancellationToken ct) =>
        {
            Result<IReadOnlyList<AlertDto>> result = await sender.Send(
                new GetAlertsQuery(severity, active ?? false), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Alerts);

        // POST /api/alerts/{id}/ack — engineer+ can acknowledge
        app.MapPost("alerts/{id}/ack", [Authorize(Policy = Policies.RequireEngineer)]
            async (string id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            string actor = user.FindFirstValue("handle") ?? "unknown";
            Result result = await sender.Send(new AcknowledgeAlertCommand(id, actor), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Alerts);
    }
}
