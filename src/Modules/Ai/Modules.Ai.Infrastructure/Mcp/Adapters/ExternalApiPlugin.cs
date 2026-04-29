using Modules.Ai.Application.Mcp.Contracts;

namespace Modules.Ai.Infrastructure.Mcp.Adapters;

/// <summary>
/// Adapter skeleton for a third-party operational REST API (CRM, billing, ticketing,
/// telco vendor systems, ...). Subclass and call the upstream over <see cref="HttpClient"/>
/// inside <see cref="InvokeAsync"/>. Wrap the upstream payload as the
/// <see cref="McpInvocationResult.Output"/> so the LLM can synthesise it back to the operator.
/// </summary>
public abstract class ExternalApiPlugin : IMcpPlugin
{
    protected ExternalApiPlugin(string pluginId, string displayName, Uri baseAddress)
    {
        PluginId = pluginId;
        DisplayName = displayName;
        BaseAddress = baseAddress;
    }

    public string PluginId { get; }
    public string DisplayName { get; }
    public McpPluginKind Kind => McpPluginKind.ExternalApi;
    public Uri BaseAddress { get; }
    public abstract IReadOnlyList<McpCapability> Capabilities { get; }

    public abstract Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default);
}
