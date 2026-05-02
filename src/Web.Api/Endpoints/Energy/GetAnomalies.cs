using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Anomalies.GetAnomalies;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetAnomalies : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("energy/anomalies", [Authorize] async (
            int? take, ISender sender, CancellationToken ct) =>
        {
            Result<AnomaliesResponse> result = await sender.Send(new GetAnomaliesQuery(take ?? 50), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
