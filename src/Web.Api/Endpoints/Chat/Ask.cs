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
            string handle = user.FindFirstValue("handle") ?? "anonymous";
            string role   = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";

            Result<CopilotAnswer> result = await sender.Send(new AskCopilotCommand(request.Query, handle, role), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Chat);
    }

    public sealed record Request(string Query);
}
