using Modules.Ai.Application.Mcp.Contracts;

namespace Modules.Ai.Infrastructure.Mcp.Adapters;

/// <summary>
/// Adapter skeleton for an external MCP server reached over HTTP/SSE. Subclass and
/// fill in <see cref="InvokeAsync"/> with the chosen transport (WebSocket, JSON-RPC,
/// streamable HTTP, ...). This lives behind <see cref="IMcpPlugin"/> so the registry
/// and the AI orchestrator can't tell the difference between an external server, an
/// internal plugin, or a vendor REST API.
/// </summary>
public abstract class ExternalMcpServerPlugin : IMcpPlugin
{
    protected ExternalMcpServerPlugin(string pluginId, string displayName, Uri serverUri)
    {
        PluginId = pluginId;
        DisplayName = displayName;
        ServerUri = serverUri;
    }

    public string PluginId { get; }
    public string DisplayName { get; }
    public McpPluginKind Kind => McpPluginKind.ExternalMcpServer;
    public Uri ServerUri { get; }
    public abstract IReadOnlyList<McpCapability> Capabilities { get; }

    public abstract Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default);
}
