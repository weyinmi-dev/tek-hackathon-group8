using Modules.Ai.Application.Mcp.Contracts;
using Modules.Ai.Application.Mcp.Registry;

namespace Modules.Ai.Infrastructure.Mcp.Registry;

internal sealed class McpPluginRegistry : IMcpPluginRegistry
{
    private readonly Dictionary<string, IMcpPlugin> _byId;

    public McpPluginRegistry(IEnumerable<IMcpPlugin> plugins)
    {
        _byId = plugins.ToDictionary(p => p.PluginId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IMcpPlugin> Plugins => _byId.Values;

    public bool TryGet(string pluginId, out IMcpPlugin plugin)
    {
        if (_byId.TryGetValue(pluginId, out IMcpPlugin? p))
        {
            plugin = p;
            return true;
        }
        plugin = null!;
        return false;
    }

    public IMcpPlugin? Find(string pluginId) => _byId.GetValueOrDefault(pluginId);
}
