using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Analytics.Api;
using Modules.Identity.Application.Authorization;
using Modules.Identity.Application.Users.ChangeRole;
using Modules.Identity.Application.Users.CreateUser;
using Modules.Identity.Application.Users.DeleteUser;
using Modules.Identity.Application.Users.ListUsers;
using Modules.Identity.Application.Users.SetActive;
using Modules.Identity.Application.Users.UpdateUser;
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

        // POST /api/auth/users → manager+ can create users (managers cannot mint Admins).
        app.MapPost("auth/users", [Authorize(Policy = Policies.RequireManager)]
            async (CreateUserRequest body, ClaimsPrincipal principal, ISender sender, IAnalyticsApi audit,
                   HttpContext http, CancellationToken ct) =>
        {
            string actorRole = principal.FindFirstValue(ClaimTypes.Role) ?? Roles.Engineer;
            string actorHandle = principal.FindFirstValue("handle") ?? "unknown";

            Result<CreatedUserDto> result = await sender.Send(new CreateUserCommand(
                body.Email, body.Password, body.FullName, body.Handle, body.Role, body.Team, body.Region, actorRole), ct);

            if (result.IsSuccess)
            {
                await audit.RecordAsync(actorHandle, actorRole, "user.create", $"{result.Value.Email}|role={result.Value.Role}", ClientIp(http), ct);
            }
            return result.Match(v => Results.Created($"/api/auth/users/{v.Id}", v), CustomResults.Problem);
        })
        .WithTags(Tags.Auth);

        // PUT /api/auth/users/{id} → manager+ can edit user profile (not the role).
        app.MapPut("auth/users/{id:guid}", [Authorize(Policy = Policies.RequireManager)]
            async (Guid id, UpdateUserRequest body, ClaimsPrincipal principal, ISender sender, IAnalyticsApi audit,
                   HttpContext http, CancellationToken ct) =>
        {
            string actorRole = principal.FindFirstValue(ClaimTypes.Role) ?? Roles.Engineer;
            string actorHandle = principal.FindFirstValue("handle") ?? "unknown";

            Result result = await sender.Send(new UpdateUserCommand(
                id, body.FullName, body.Handle, body.Team, body.Region, actorRole), ct);

            if (result.IsSuccess)
            {
                await audit.RecordAsync(actorHandle, actorRole, "user.update", id.ToString(), ClientIp(http), ct);
            }
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Auth);

        // PUT /api/auth/users/{id}/role → admin or manager (managers can't touch Admins).
        app.MapPut("auth/users/{id:guid}/role", [Authorize(Policy = Policies.RequireManager)]
            async (Guid id, ChangeRoleRequest body, ClaimsPrincipal principal, ISender sender, IAnalyticsApi audit,
                   HttpContext http, CancellationToken ct) =>
        {
            string actorRole = principal.FindFirstValue(ClaimTypes.Role) ?? Roles.Engineer;
            string actorHandle = principal.FindFirstValue("handle") ?? "unknown";
            Guid actorId = ParseActorId(principal);

            Result result = await sender.Send(new ChangeUserRoleCommand(id, body.Role, actorRole, actorId), ct);

            if (result.IsSuccess)
            {
                await audit.RecordAsync(actorHandle, actorRole, "user.role.change", $"{id}|to={body.Role}", ClientIp(http), ct);
            }
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Auth);

        // PUT /api/auth/users/{id}/active → admin or manager (managers can't touch Admins).
        app.MapPut("auth/users/{id:guid}/active", [Authorize(Policy = Policies.RequireManager)]
            async (Guid id, SetActiveRequest body, ClaimsPrincipal principal, ISender sender, IAnalyticsApi audit,
                   HttpContext http, CancellationToken ct) =>
        {
            string actorRole = principal.FindFirstValue(ClaimTypes.Role) ?? Roles.Engineer;
            string actorHandle = principal.FindFirstValue("handle") ?? "unknown";
            Guid actorId = ParseActorId(principal);

            Result result = await sender.Send(new SetUserActiveCommand(id, body.IsActive, actorRole, actorId), ct);

            if (result.IsSuccess)
            {
                string action = body.IsActive ? "user.activate" : "user.deactivate";
                await audit.RecordAsync(actorHandle, actorRole, action, id.ToString(), ClientIp(http), ct);
            }
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Auth);

        // DELETE /api/auth/users/{id} → admin only — hard delete.
        app.MapDelete("auth/users/{id:guid}", [Authorize(Policy = Policies.RequireAdmin)]
            async (Guid id, ClaimsPrincipal principal, ISender sender, IAnalyticsApi audit,
                   HttpContext http, CancellationToken ct) =>
        {
            string actorRole = principal.FindFirstValue(ClaimTypes.Role) ?? Roles.Admin;
            string actorHandle = principal.FindFirstValue("handle") ?? "unknown";
            Guid actorId = ParseActorId(principal);

            Result result = await sender.Send(new DeleteUserCommand(id, actorId), ct);

            if (result.IsSuccess)
            {
                await audit.RecordAsync(actorHandle, actorRole, "user.delete", id.ToString(), ClientIp(http), ct);
            }
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Auth);
    }

    private static Guid ParseActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? principal.FindFirstValue("sub"), out Guid id) ? id : Guid.Empty;

    private static string ClientIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public sealed record CreateUserRequest(
        string Email,
        string Password,
        string FullName,
        string Handle,
        string Role,
        string Team,
        string Region);

    public sealed record UpdateUserRequest(
        string FullName,
        string Handle,
        string Team,
        string Region);

    public sealed record ChangeRoleRequest(string Role);

    public sealed record SetActiveRequest(bool IsActive);
}
