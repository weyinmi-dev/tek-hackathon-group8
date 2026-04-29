namespace Modules.Ai.Application.Mcp.Contracts;

/// <summary>
/// Provider-agnostic plugin contract. Every MCP integration — internal modules,
/// external MCP servers, vendor REST APIs — implements this same shape and is
/// registered in <c>IMcpPluginRegistry</c>. The Copilot orchestrator never sees
/// the underlying transport.
/// </summary>
public interface IMcpPlugin
{
    /// <summary>Stable plugin identifier — used as the registry key and for audit attribution.</summary>
    string PluginId { get; }

    /// <summary>Human-readable name for the operator UI.</summary>
    string DisplayName { get; }

    /// <summary>Where the plugin runs — drives the transport choice.</summary>
    McpPluginKind Kind { get; }

    /// <summary>Capabilities advertised to the LLM and the document UI.</summary>
    IReadOnlyList<McpCapability> Capabilities { get; }

    Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default);
}
