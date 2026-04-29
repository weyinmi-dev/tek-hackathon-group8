using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Ai.Application.Copilot.AskCopilot;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Chat;

public sealed class Ask : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Copilot is open to every authenticated user — viewers, engineers, managers, admins.
        // The handler still receives the caller's role so responses can be tailored downstream.
        // Conversation persistence: pass conversationId to continue a session, omit to start one.
        app.MapPost("chat", [Authorize]
            async (Request request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            string trimmedQuery = request.Query.Trim();

            if (string.IsNullOrWhiteSpace(trimmedQuery) || trimmedQuery.Length > 500)
            {
                return Results.Problem(
                    title: "Invalid Query",
                    detail: "Query must be 1-500 characters.",
                    statusCode: 400,
                    type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
            }

            string handle = user.FindFirstValue("handle") ?? "anonymous";
            string role   = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";
            Guid userId   = ParseUserId(user);

            Result<CopilotAnswer> result = await sender.Send(
                new AskCopilotCommand(trimmedQuery, userId, handle, role, request.ConversationId), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Chat);
    }

    private static Guid ParseUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user.FindFirstValue("sub"), out Guid id) ? id : Guid.Empty;

    public sealed record Request(string Query, Guid? ConversationId);
}
