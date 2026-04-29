using System.Globalization;
using Modules.Ai.Application.Mcp.Contracts;
using Modules.Network.Api;

namespace Modules.Ai.Infrastructure.Mcp.Plugins;

/// <summary>
/// Demo internal MCP plugin — exposes the Network module's read-side capabilities
/// through the MCP contract. Demonstrates the in-process adapter pattern: the
/// plugin lives inside the monolith and dispatches through a sibling module's
/// public <c>.Api</c> interface, never an HTTP hop.
/// </summary>
internal sealed class NetworkMcpPlugin(INetworkApi network) : IMcpPlugin
{
    public string PluginId => "network";
    public string DisplayName => "Network Monitoring";
    public McpPluginKind Kind => McpPluginKind.Internal;

    public IReadOnlyList<McpCapability> Capabilities { get; } =
    [
        new McpCapability(
            "list_towers",
            "List every tower's current snapshot (signal, load, status).",
            []),
        new McpCapability(
            "list_by_region",
            "List the towers in a specific Lagos region.",
            [new McpCapabilityParameter("region", "string", "Region name, e.g. 'Lekki', 'Ikeja'.", IsRequired: true)]),
        new McpCapability(
            "region_health",
            "Aggregate signal + critical/warn counts per region.",
            []),
    ];

    public async Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request.Capability.ToLowerInvariant())
        {
            case "list_towers":
            {
                IReadOnlyList<TowerSnapshot> towers = await network.ListTowersAsync(cancellationToken);
                return Ok(request, towers);
            }
            case "list_by_region":
            {
                string region = ReadString(request.Arguments, "region");
                IReadOnlyList<TowerSnapshot> towers = await network.ListByRegionAsync(region, cancellationToken);
                return Ok(request, towers);
            }
            case "region_health":
            {
                IReadOnlyList<RegionHealth> health = await network.GetRegionHealthAsync(cancellationToken);
                return Ok(request, health);
            }
            default:
                return new McpInvocationResult(
                    request.PluginId, request.Capability, IsSuccess: false,
                    Output: null, Error: $"Unknown capability '{request.Capability}'.",
                    DurationMs: 0, CorrelationId: request.CorrelationId);
        }
    }

    private static McpInvocationResult Ok(McpInvocationRequest request, object output) =>
        new(request.PluginId, request.Capability, IsSuccess: true,
            Output: output, Error: null, DurationMs: 0, CorrelationId: request.CorrelationId);

    private static string ReadString(IReadOnlyDictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out object? value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
}
