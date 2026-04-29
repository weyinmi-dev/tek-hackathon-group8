namespace Modules.Ai.Application.Mcp.Contracts;

/// <summary>
/// Where the plugin's logic actually lives. Drives the registry's choice of
/// transport adapter (in-process call vs HTTP MCP server vs vendor REST API).
/// </summary>
public enum McpPluginKind
{
    /// <summary>The plugin's <see cref="IMcpPlugin.InvokeAsync"/> runs in this process.</summary>
    Internal = 0,

    /// <summary>The plugin proxies an external MCP server (typically over HTTP/SSE).</summary>
    ExternalMcpServer = 1,

    /// <summary>The plugin wraps a third-party operational API (CRM, billing, ticketing).</summary>
    ExternalApi = 2,
}
