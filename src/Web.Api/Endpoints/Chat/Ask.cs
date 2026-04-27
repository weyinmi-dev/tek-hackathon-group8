using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Ai.Application.Copilot.AskCopilot;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Chat;

public sealed class Ask : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("chat", [Authorize(Policy = Policies.RequireEngineer)]
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

            Result<CopilotAnswer> result = await sender.Send(new AskCopilotCommand(trimmedQuery, handle, role), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Chat);
    }

    public sealed record Request(string Query);
}
