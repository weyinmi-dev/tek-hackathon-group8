using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Anomalies.AcknowledgeAnomaly;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class AcknowledgeAnomaly : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // POST /api/energy/anomalies/{id}/ack — engineer+ (mirrors alerts/{id}/ack policy).
        app.MapPost("energy/anomalies/{id:guid}/ack",
            [Authorize(Policy = Policies.RequireEngineer)] async (
                Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            string handle = user.FindFirstValue("handle") ?? "anonymous";
            string role = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";
            Result result = await sender.Send(new AcknowledgeAnomalyCommand(id, handle, role), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
