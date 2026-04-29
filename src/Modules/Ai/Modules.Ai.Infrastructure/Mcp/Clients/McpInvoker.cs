using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Mcp.Clients;
using Modules.Ai.Application.Mcp.Contracts;
using Modules.Ai.Application.Mcp.Registry;

namespace Modules.Ai.Infrastructure.Mcp.Clients;

internal sealed class McpInvoker(
    IMcpPluginRegistry registry,
    ILogger<McpInvoker> logger) : IMcpInvoker
{
    public async Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!registry.TryGet(request.PluginId, out IMcpPlugin plugin))
        {
            return new McpInvocationResult(
                request.PluginId, request.Capability, IsSuccess: false,
                Output: null, Error: $"Plugin '{request.PluginId}' is not registered.",
                DurationMs: 0, CorrelationId: request.CorrelationId);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            McpInvocationResult result = await plugin.InvokeAsync(request, cancellationToken);
            sw.Stop();
            return result with { DurationMs = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "MCP plugin {PluginId}/{Capability} threw", request.PluginId, request.Capability);
            return new McpInvocationResult(
                request.PluginId, request.Capability, IsSuccess: false,
                Output: null, Error: ex.Message,
                DurationMs: sw.ElapsedMilliseconds, CorrelationId: request.CorrelationId);
        }
    }
}
