using System.ComponentModel;
using MediatR;
using Modules.Ai.Application.Tools;

namespace Modules.Ai.Agents.Tools;

/// <summary>
/// Network capability tools exposed to the agents. Each method is a thin shim over a MediatR
/// query (Phase 2 §6.2); the method name becomes the tool name and the [Description] attributes
/// drive the schema the model sees.
/// </summary>
public sealed class NetworkTools(ISender sender)
{
    [Description("Return the current signal, load and status snapshot for every tower in a region. Pass an empty string for all regions.")]
    public Task<string> GetRegionMetrics(
        [Description("Region name, e.g. 'Lekki' or 'Lagos West'. Empty for all regions.")] string region,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetRegionMetricsQuery(region), cancellationToken);

    [Description("Return the current snapshot for a single tower by its code.")]
    public Task<string> GetTowerMetrics(
        [Description("Tower code, e.g. 'LOS-T-014'.")] string towerCode,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetTowerMetricsQuery(towerCode), cancellationToken);

    [Description("Free-text search over towers by code, name or region.")]
    public Task<string> SearchTowers(
        [Description("Search text matched against tower code, name and region.")] string query,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new SearchTowersQuery(query), cancellationToken);
}
