using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Analytics.Application.Audit.GetAuditLog;
using Modules.Analytics.Application.Metrics.GetMetrics;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Metrics;

public sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/metrics → KPIs, sparklines, region health, incident-type breakdown
        app.MapGet("metrics", [Authorize] async (ISender sender, CancellationToken ct) =>
        {
            Result<MetricsResponse> result = await sender.Send(new GetMetricsQuery(), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Metrics);

        // GET /api/metrics/audit → audit log feed (sub-resource of /metrics, single Tag, no extra "resource")
        app.MapGet("metrics/audit", [Authorize(Policy = Policies.RequireManager)]
            async (int? take, ISender sender, CancellationToken ct) =>
        {
            Result<IReadOnlyList<AuditEntryDto>> result = await sender.Send(new GetAuditLogQuery(take ?? 50), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Metrics);
    }
}
