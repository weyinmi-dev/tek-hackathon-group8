using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Alerts.Application.Alerts.Acknowledge;
using Modules.Alerts.Application.Alerts.Assign;
using Modules.Alerts.Application.Alerts.Dispatch;
using Modules.Alerts.Application.Alerts.GetAlerts;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Endpoints.Geo;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Alerts;

public sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/alerts?severity=critical&active=true
        // Each alert is enriched with OSM-derived geo context (region type, accessibility
        // score, nearest fuel station) — keyed by tower code via ISiteGeoLookup. Cached
        // at the OSM client layer so repeat hits are sub-ms; warm cache means this adds
        // ~5ms over the bare query.
        //
        // Geo enrichment is decoration only: a top-level try/catch ensures even a
        // catastrophic GeoEnricher failure (DI resolution, etc.) still serves the bare
        // alert list with geo=null per item. Without this guard the dashboard's
        // Promise.all([metrics, map, alerts]) would fail-fast all three — see
        // dashboard/page.tsx for the matching frontend resilience.
        app.MapGet("alerts", [Authorize] async (
            string? severity, bool? active, ISender sender, GeoEnricher geo,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            Result<IReadOnlyList<AlertDto>> result = await sender.Send(
                new GetAlertsQuery(severity, active ?? false), ct);
            if (result.IsFailure) return CustomResults.Problem(result);

            IReadOnlyList<AlertDto> alerts = result.Value;

            IReadOnlyDictionary<string, GeoSummary> geoMap;
            try
            {
                geoMap = await geo.ForSitesAsync(alerts.Select(a => a.Tower), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Alerts.Get").LogWarning(
                    ex, "Geo batch failed; serving alerts without geo context.");
                geoMap = new Dictionary<string, GeoSummary>(StringComparer.OrdinalIgnoreCase);
            }

            List<AlertWithGeo> enriched = alerts
                .Select(a => AlertWithGeo.From(a, geoMap.GetValueOrDefault(a.Tower)))
                .ToList();
            return Results.Ok(enriched);
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

        // POST /api/alerts/{id}/assign — manager+ assigns the incident to a NOC team.
        app.MapPost("alerts/{id}/assign", [Authorize(Policy = Policies.RequireManager)]
            async (string id, AssignBody body, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            string actor = user.FindFirstValue("handle") ?? "unknown";
            string role = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";
            Result result = await sender.Send(new AssignAlertCommand(id, body.Team, actor, role), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Alerts);

        // POST /api/alerts/{id}/dispatch — engineer+ records a field dispatch (truck, vendor, etc.).
        app.MapPost("alerts/{id}/dispatch", [Authorize(Policy = Policies.RequireEngineer)]
            async (string id, DispatchBody body, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            string actor = user.FindFirstValue("handle") ?? "unknown";
            string role = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";
            Result result = await sender.Send(new DispatchAlertCommand(id, body.Target, actor, role), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Alerts);
    }

    public sealed record AssignBody(string Team);
    public sealed record DispatchBody(string Target);
}
