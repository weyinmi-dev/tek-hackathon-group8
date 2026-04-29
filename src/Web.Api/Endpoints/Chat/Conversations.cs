using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Ai.Application.Copilot.Conversations;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Chat;

public sealed class Conversations : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/chat/conversations → sidebar listing for the signed-in user
        app.MapGet("chat/conversations", [Authorize]
            async (ClaimsPrincipal user, ISender sender, int? take, CancellationToken ct) =>
        {
            Guid userId = ParseUserId(user);
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }
            Result<IReadOnlyList<ConversationSummary>> result =
                await sender.Send(new ListConversationsQuery(userId, take ?? 50), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Chat);

        // GET /api/chat/conversations/{id} → full message history for replay on session restore
        app.MapGet("chat/conversations/{id:guid}", [Authorize]
            async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            Guid userId = ParseUserId(user);
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }
            Result<ConversationDetail> result = await sender.Send(new GetConversationQuery(id, userId), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Chat);

        // PATCH /api/chat/conversations/{id} → rename
        app.MapPatch("chat/conversations/{id:guid}", [Authorize]
            async (Guid id, RenameRequest body, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            Guid userId = ParseUserId(user);
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }
            Result result = await sender.Send(new RenameConversationCommand(id, userId, body.Title), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Chat);

        // DELETE /api/chat/conversations/{id} → user-initiated deletion (cascades messages)
        app.MapDelete("chat/conversations/{id:guid}", [Authorize]
            async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            Guid userId = ParseUserId(user);
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }
            Result result = await sender.Send(new DeleteConversationCommand(id, userId), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Chat);
    }

    private static Guid ParseUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user.FindFirstValue("sub"), out Guid id) ? id : Guid.Empty;

    public sealed record RenameRequest(string Title);
}
