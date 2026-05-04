using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Optimization.Recommendations;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetRecommendations : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Optional ?site=TWR-... narrows recommendations to one site (used by Copilot's MCP path).
        app.MapGet("energy/recommendations", [Authorize] async (
            string? site, ISender sender, CancellationToken ct) =>
        {
            Result<RecommendationsResponse> result = await sender.Send(new GetRecommendationsQuery(site), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
