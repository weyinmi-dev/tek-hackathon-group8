using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Identity.Application.Authorization;
using Modules.Identity.Application.Users.ListUsers;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

public sealed class Users : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/auth/users → manager+ can browse the user/RBAC roster
        app.MapGet("auth/users", [Authorize(Policy = Policies.RequireManager)]
            async (ISender sender, CancellationToken ct) =>
        {
            Result<IReadOnlyList<UserListItem>> result = await sender.Send(new ListUsersQuery(), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Auth);
    }
}
