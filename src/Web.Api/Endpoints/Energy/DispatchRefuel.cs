using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Sites.DispatchRefuel;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class DispatchRefuel : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Operator records a fuel delivery. Bumps DieselPct, writes a FuelEvent + audit row.
        app.MapPost("energy/sites/{code}/refuel",
            [Authorize(Policy = Policies.RequireEngineer)] async (
                string code, Request body, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            string handle = user.FindFirstValue("handle") ?? "anonymous";
            string role = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";
            Result<DispatchRefuelResponse> result = await sender.Send(
                new DispatchRefuelCommand(code, body.LitresAdded, handle, role), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }

    public sealed record Request(int LitresAdded);
}
