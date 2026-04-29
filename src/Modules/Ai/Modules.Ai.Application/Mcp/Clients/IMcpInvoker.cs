using Modules.Ai.Application.Mcp.Contracts;

namespace Modules.Ai.Application.Mcp.Clients;

/// <summary>
/// Thin façade the Copilot (and any other caller) goes through to invoke a
/// plugin. Resolves the plugin via the registry, attaches diagnostics, and
/// captures audit traces. Splits "find a plugin" from "invoke a plugin" so the
/// AI orchestrator stays unaware of the registry mechanics.
/// </summary>
public interface IMcpInvoker
{
    Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default);
}
