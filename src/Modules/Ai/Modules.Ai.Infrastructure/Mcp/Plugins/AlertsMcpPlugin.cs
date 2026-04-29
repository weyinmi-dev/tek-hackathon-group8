using Modules.Ai.Application.Mcp.Contracts;
using Modules.Alerts.Api;

namespace Modules.Ai.Infrastructure.Mcp.Plugins;

internal sealed class AlertsMcpPlugin(IAlertsApi alerts) : IMcpPlugin
{
    public string PluginId => "alerts";
    public string DisplayName => "Smart Alerts";
    public McpPluginKind Kind => McpPluginKind.Internal;

    public IReadOnlyList<McpCapability> Capabilities { get; } =
    [
        new McpCapability("list_active",
            "Active alerts (not yet acknowledged), grouped by severity.",
            []),
        new McpCapability("list_all",
            "Every alert in the system, including acknowledged history.",
            []),
    ];

    public async Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Capability.ToLowerInvariant() switch
        {
            "list_active" => Ok(request, await alerts.ListActiveAsync(cancellationToken)),
            "list_all" => Ok(request, await alerts.ListAllAsync(cancellationToken)),
            _ => new McpInvocationResult(request.PluginId, request.Capability, false, null,
                $"Unknown capability '{request.Capability}'.", 0, request.CorrelationId),
        };
    }

    private static McpInvocationResult Ok(McpInvocationRequest request, object output) =>
        new(request.PluginId, request.Capability, IsSuccess: true,
            Output: output, Error: null, DurationMs: 0, CorrelationId: request.CorrelationId);
}
