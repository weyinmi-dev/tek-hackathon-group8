using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Modules.Ai.Application.Mcp.Clients;
using Modules.Ai.Application.Mcp.Contracts;
using Modules.Ai.Application.Mcp.Registry;
using Modules.Analytics.Api;
using Modules.Identity.Application.Authorization;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Mcp;

public sealed class Mcp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/mcp/plugins → engineer+ enumerate available plugins (live + their capabilities).
        app.MapGet("mcp/plugins", [Authorize(Policy = Policies.RequireEngineer)]
            (IMcpPluginRegistry registry) =>
        {
            object[] plugins = registry.Plugins.Select(p => new
            {
                pluginId = p.PluginId,
                displayName = p.DisplayName,
                kind = p.Kind.ToString(),
                capabilities = p.Capabilities.Select(c => new
                {
                    name = c.Name,
                    description = c.Description,
                    parameters = c.Parameters.Select(prm => new
                    {
                        name = prm.Name,
                        type = prm.Type,
                        description = prm.Description,
                        required = prm.IsRequired,
                    }),
                }),
            }).ToArray<object>();
            return Results.Ok(plugins);
        })
        .WithTags(Tags.Mcp);

        // POST /api/mcp/invoke → manager+ run a plugin capability. Audited.
        app.MapPost("mcp/invoke", [Authorize(Policy = Policies.RequireManager)]
            async (InvokeRequest body, ClaimsPrincipal principal, IMcpInvoker invoker, IAnalyticsApi audit,
                   HttpContext http, CancellationToken ct) =>
        {
            string actor = principal.FindFirstValue("handle") ?? "unknown";
            string role = principal.FindFirstValue(ClaimTypes.Role) ?? Roles.Manager;

            var request = new McpInvocationRequest(
                body.PluginId,
                body.Capability,
                body.Arguments ?? new Dictionary<string, object?>(),
                CorrelationId: body.CorrelationId);

            McpInvocationResult result = await invoker.InvokeAsync(request, ct);

            await audit.RecordAsync(actor, role, $"mcp.invoke:{body.PluginId}/{body.Capability}",
                $"success={result.IsSuccess}|ms={result.DurationMs}", ClientIp(http), ct);

            return Results.Ok(result);
        })
        .WithTags(Tags.Mcp);
    }

    private static string ClientIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public sealed record InvokeRequest(
        string PluginId,
        string Capability,
        Dictionary<string, object?>? Arguments,
        string? CorrelationId);
}
