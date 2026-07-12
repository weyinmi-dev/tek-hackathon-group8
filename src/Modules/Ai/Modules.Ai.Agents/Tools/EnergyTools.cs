using System.ComponentModel;
using MediatR;
using Modules.Ai.Application.Tools;

namespace Modules.Ai.Agents.Tools;

/// <summary>Energy/power capability tools (Phase 2 §6.2).</summary>
public sealed class EnergyTools(ISender sender)
{
    [Description("Return fleet-wide energy and power KPIs.")]
    public Task<string> GetEnergyKpis(CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetEnergyKpisQuery(), cancellationToken);

    [Description("Return the most recent energy anomalies (fuel theft, battery health, diesel consumption).")]
    public Task<string> DetectEnergyAnomalies(
        [Description("Maximum number of anomalies to return.")] int take = 50,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new DetectEnergyAnomaliesQuery(take), cancellationToken);

    [Description("Return the diesel-level trace for a site over the last N hours.")]
    public Task<string> GetDieselTrace(
        [Description("Site code, e.g. 'ENG-LEK-01'.")] string siteCode,
        [Description("Hours of history to include.")] int hours = 24,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetDieselTraceQuery(siteCode, hours), cancellationToken);
}
