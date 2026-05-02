using System.Globalization;
using Modules.Ai.Application.Mcp.Contracts;
using Modules.Energy.Api;

namespace Modules.Ai.Infrastructure.Mcp.Plugins;

/// <summary>
/// Internal MCP plugin for the Energy module. Lets the Copilot answer questions like
/// "Which sites are at risk of fuel theft?" or "Recommend cost optimizations for Lekki cluster"
/// by invoking real backend queries instead of inventing data.
///
/// All capabilities are read-only and engineer+ visible — the gating happens upstream at the
/// /api/mcp/invoke endpoint via [Authorize] policy. Within the orchestrator, SK auto-function-
/// calling can fire any of these tools when a user query mentions energy, diesel, theft, or
/// optimization.
/// </summary>
internal sealed class EnergyMcpPlugin(IEnergyApi energy) : IMcpPlugin
{
    public string PluginId => "energy";
    public string DisplayName => "Energy Orchestration";
    public McpPluginKind Kind => McpPluginKind.Internal;

    public IReadOnlyList<McpCapability> Capabilities { get; } =
    [
        new McpCapability(
            "get_sites_overview",
            "List every base-station site with its current power source, battery %, diesel %, " +
            "solar output, grid status, daily cost (₦), and health rating.",
            []),
        new McpCapability(
            "get_site_diagnostics",
            "Deep state for a single site, including any open anomaly note. Use when the user " +
            "asks 'why is X happening at site Y' or wants per-site detail.",
            [new McpCapabilityParameter("site_code", "string", "Tower / site code, e.g. 'TWR-LAG-W-014'.", IsRequired: true)]),
        new McpCapability(
            "get_kpis",
            "Fleet-wide energy KPIs: 24h diesel litres, daily OPEX, sites on solar, fleet uptime, " +
            "theft events in the last 7 days, and average battery health.",
            []),
        new McpCapability(
            "detect_anomalies",
            "Recent anomaly detections (fuel theft, sensor offline, gen overuse, battery degradation, " +
            "predicted faults). Includes confidence and the model that produced each detection.",
            [new McpCapabilityParameter("take", "integer", "How many recent anomalies to return (default 20).", IsRequired: false)]),
        new McpCapability(
            "get_site_diesel_trace",
            "24h diesel-level trace for a site — used to explain consumption spikes or theft signatures.",
            [new McpCapabilityParameter("site_code", "string", "Tower / site code.", IsRequired: true),
             new McpCapabilityParameter("hours", "integer", "Window in hours (1-72, default 24).", IsRequired: false)]),
        new McpCapability(
            "recommend_optimizations",
            "Ranked, actionable cost-optimization recommendations derived from current site state. " +
            "Optionally narrow to a single site by passing site_code.",
            [new McpCapabilityParameter("site_code", "string", "Optional tower / site code to narrow scope.", IsRequired: false)]),
    ];

    public async Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request.Capability.ToLowerInvariant())
        {
            case "get_sites_overview":
            {
                IReadOnlyList<SiteSnapshot> sites = await energy.ListSitesAsync(cancellationToken);
                return Ok(request, sites);
            }
            case "get_site_diagnostics":
            {
                string code = ReadString(request.Arguments, "site_code");
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Fail(request, "site_code is required.");
                }
                SiteSnapshot? site = await energy.GetSiteAsync(code, cancellationToken);
                return site is null
                    ? Fail(request, $"Site '{code}' not found.")
                    : Ok(request, site);
            }
            case "get_kpis":
            {
                EnergyKpiSnapshot kpis = await energy.GetKpisAsync(cancellationToken);
                return Ok(request, kpis);
            }
            case "detect_anomalies":
            {
                int take = ReadInt(request.Arguments, "take", 20);
                IReadOnlyList<AnomalySnapshot> anomalies = await energy.ListAnomaliesAsync(take, cancellationToken);
                return Ok(request, anomalies);
            }
            case "get_site_diesel_trace":
            {
                string code = ReadString(request.Arguments, "site_code");
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Fail(request, "site_code is required.");
                }
                int hours = Math.Clamp(ReadInt(request.Arguments, "hours", 24), 1, 72);
                IReadOnlyList<DieselTracePoint> points = await energy.GetSiteDieselTraceAsync(code, hours, cancellationToken);
                return Ok(request, points);
            }
            case "recommend_optimizations":
            {
                string? code = ReadString(request.Arguments, "site_code");
                if (string.IsNullOrWhiteSpace(code)) code = null;
                IReadOnlyList<RecommendationSnapshot> recs = await energy.RecommendOptimizationsAsync(code, cancellationToken);
                return Ok(request, recs);
            }
            default:
                return Fail(request, $"Unknown capability '{request.Capability}'.");
        }
    }

    private static McpInvocationResult Ok(McpInvocationRequest request, object output) =>
        new(request.PluginId, request.Capability, IsSuccess: true,
            Output: output, Error: null, DurationMs: 0, CorrelationId: request.CorrelationId);

    private static McpInvocationResult Fail(McpInvocationRequest request, string error) =>
        new(request.PluginId, request.Capability, IsSuccess: false,
            Output: null, Error: error, DurationMs: 0, CorrelationId: request.CorrelationId);

    private static string ReadString(IReadOnlyDictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out object? value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

    private static int ReadInt(IReadOnlyDictionary<string, object?> args, string key, int fallback)
    {
        if (!args.TryGetValue(key, out object? value) || value is null) return fallback;
        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => fallback,
        };
    }
}
