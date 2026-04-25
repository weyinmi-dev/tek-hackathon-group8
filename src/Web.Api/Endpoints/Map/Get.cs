using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Network.Application.Map.GetMap;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Map;

public sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/map → tower positions, status, region health rollup
        app.MapGet("map", [Authorize] async (ISender sender, CancellationToken ct) =>
        {
            Result<MapResponse> result = await sender.Send(new GetMapQuery(), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Map);
    }
}
