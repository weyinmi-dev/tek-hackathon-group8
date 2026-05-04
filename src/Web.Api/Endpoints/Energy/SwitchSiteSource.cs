using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Sites.SwitchSource;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class SwitchSiteSource : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Engineer+ can flip a site's active power source. The handler audits the action.
        app.MapPost("energy/sites/{code}/switch-source",
            [Authorize(Policy = Policies.RequireEngineer)] async (
                string code, Request body, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            string handle = user.FindFirstValue("handle") ?? "anonymous";
            string role = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";
            Result<SwitchSiteSourceResponse> result = await sender.Send(
                new SwitchSiteSourceCommand(code, body.Source, handle, role), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }

    public sealed record Request(string Source);
}
