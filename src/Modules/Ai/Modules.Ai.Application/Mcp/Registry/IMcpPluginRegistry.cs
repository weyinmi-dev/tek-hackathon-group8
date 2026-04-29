using Modules.Ai.Application.Mcp.Contracts;

namespace Modules.Ai.Application.Mcp.Registry;

/// <summary>
/// Look-up surface for the registered MCP plugins. The Copilot orchestrator
/// resolves a plugin by id, the discovery endpoint enumerates them, and the
/// invocation pipeline dispatches through this single seam — that's how we
/// keep the contract testable and the transports interchangeable.
/// </summary>
public interface IMcpPluginRegistry
{
    IReadOnlyCollection<IMcpPlugin> Plugins { get; }

    bool TryGet(string pluginId, out IMcpPlugin plugin);

    /// <summary>Convenience — returns null instead of throwing when the plugin is unknown.</summary>
    IMcpPlugin? Find(string pluginId);
}
