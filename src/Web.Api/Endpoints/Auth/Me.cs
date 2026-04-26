using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Web.Api.Endpoints.Auth;

public sealed class Me : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("auth/me", [Authorize] (ClaimsPrincipal user) => Results.Ok(new
        {
            id     = user.FindFirstValue("sub"),
            email  = user.FindFirstValue("email"),
            name   = user.FindFirstValue("name"),
            handle = user.FindFirstValue("handle"),
            role   = user.FindFirstValue(ClaimTypes.Role),
            team   = user.FindFirstValue("team"),
            region = user.FindFirstValue("region"),
        }))
        .WithTags(Tags.Auth);
    }
}
