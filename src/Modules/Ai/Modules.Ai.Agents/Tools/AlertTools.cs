using System.ComponentModel;
using MediatR;
using Modules.Ai.Application.Tools;

namespace Modules.Ai.Agents.Tools;

/// <summary>Alert/incident capability tools (Phase 2 §6.2).</summary>
public sealed class AlertTools(ISender sender)
{
    [Description("Return active or recent incidents across the metro.")]
    public Task<string> GetActiveOutages(CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetActiveOutagesQuery(), cancellationToken);

    [Description("Search all alerts (active and resolved), optionally filtered to a region.")]
    public Task<string> SearchAlarmHistory(
        [Description("Region name to filter to. Empty for all regions.")] string region = "",
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(
            sender,
            new SearchAlarmHistoryQuery(string.IsNullOrWhiteSpace(region) ? null : region),
            cancellationToken);
}
