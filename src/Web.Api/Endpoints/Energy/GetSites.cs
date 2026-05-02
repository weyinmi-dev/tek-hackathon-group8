using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Sites.GetSites;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetSites : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/energy/sites → fleet table for the Energy Sites page.
        // Open to every authenticated user; the page is read-only here.
        app.MapGet("energy/sites", [Authorize] async (ISender sender, CancellationToken ct) =>
        {
            Result<SitesResponse> result = await sender.Send(new GetSitesQuery(), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
