using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Sites.GetEnergyMetrics;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetMetrics : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/energy/metrics → energy-side analytics for the Operations Dashboard:
        // regional health, source mix, anomaly type breakdown, OPEX trend, top diesel
        // burners. Mirrors the shape of /api/metrics so the frontend can render an
        // "Energy" panel alongside the "Ops" panel.
        app.MapGet("energy/metrics", [Authorize] async (ISender sender, CancellationToken ct) =>
        {
            Result<EnergyMetricsResponse> result = await sender.Send(new GetEnergyMetricsQuery(), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
