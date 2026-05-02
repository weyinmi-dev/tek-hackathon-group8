using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Modules.Energy.Api;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill that exposes the Energy module to auto-function-calling. Each function maps
/// 1:1 to an <see cref="IEnergyApi"/> capability (same surface as <c>EnergyMcpPlugin</c>);
/// the LLM picks the right one based on the user query and the [Description] hints.
///
/// Per the orchestrator's "MCP for actions, RAG for explanations" rule, these tools are
/// the action / live-state path. Long-form historical reasoning ("why did Surulere consume
/// more diesel yesterday?") still flows through KnowledgeSkill (RAG), then optionally
/// chains here for the latest snapshot.
/// </summary>
public sealed class EnergySkill(IEnergyApi energy)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    [KernelFunction("get_energy_sites")]
    [Description("Tool: get_energy_sites. Returns every base-station site with its current power source (grid/generator/battery/solar), battery %, diesel %, solar kW, daily diesel litres, daily ₦ cost, uptime %, and health rating (ok/degraded/critical). Use this for fleet-wide energy questions.")]
    public async Task<string> GetSitesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SiteSnapshot> sites = await energy.ListSitesAsync(cancellationToken);
        return JsonSerializer.Serialize(sites, JsonOpts);
    }

    [KernelFunction("get_energy_site")]
    [Description("Tool: get_energy_site. Returns the live state of a single site by code (e.g. 'TWR-LEK-003'). Use when the user asks about a specific site.")]
    public async Task<string> GetSiteAsync(
        [Description("Tower / site code, e.g. 'TWR-LAG-W-014'.")] string siteCode,
        CancellationToken cancellationToken = default)
    {
        SiteSnapshot? site = await energy.GetSiteAsync(siteCode ?? "", cancellationToken);
        if (site is null)
        {
            return JsonSerializer.Serialize(new { error = "site_not_found", message = $"Site '{siteCode}' not found." }, JsonOpts);
        }
        return JsonSerializer.Serialize(site, JsonOpts);
    }

    [KernelFunction("get_energy_kpis")]
    [Description("Tool: get_energy_kpis. Fleet-wide energy KPIs: 24h diesel litres, daily OPEX ₦, sites on solar, fleet uptime %, theft events in the last 7 days, and average battery health. Use for executive-level summaries.")]
    public async Task<string> GetKpisAsync(CancellationToken cancellationToken = default)
    {
        EnergyKpiSnapshot kpis = await energy.GetKpisAsync(cancellationToken);
        return JsonSerializer.Serialize(kpis, JsonOpts);
    }

    [KernelFunction("detect_energy_anomalies")]
    [Description("Tool: detect_energy_anomalies. Recent anomaly detections (fuel theft, sensor offline, generator overuse, battery degradation, predicted faults), with confidence and the model that produced each detection. Use when asked about theft, faults, or unusual energy patterns.")]
    public async Task<string> DetectAnomaliesAsync(
        [Description("Maximum events to return (1-200, default 20).")] int take = 20,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AnomalySnapshot> anomalies = await energy.ListAnomaliesAsync(Math.Clamp(take, 1, 200), cancellationToken);
        return JsonSerializer.Serialize(anomalies, JsonOpts);
    }

    [KernelFunction("get_energy_diesel_trace")]
    [Description("Tool: get_energy_diesel_trace. 24h diesel-level trace for a site — useful to explain consumption spikes or theft signatures (sharp drops outside refill windows).")]
    public async Task<string> GetDieselTraceAsync(
        [Description("Tower / site code.")] string siteCode,
        [Description("Window in hours (1-72, default 24).")] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DieselTracePoint> points = await energy.GetSiteDieselTraceAsync(
            siteCode ?? "", Math.Clamp(hours, 1, 72), cancellationToken);
        return JsonSerializer.Serialize(points, JsonOpts);
    }

    [KernelFunction("recommend_energy_optimizations")]
    [Description("Tool: recommend_energy_optimizations. Ranked, actionable cost-optimization recommendations derived from current site state (e.g. 'Convert 12 high-diesel sites to hybrid solar'). Optionally narrow to a site by passing site_code. Use when the user asks for cost savings or what to do next.")]
    public async Task<string> RecommendAsync(
        [Description("Optional site code to narrow recommendations to a single site.")] string? siteCode = null,
        CancellationToken cancellationToken = default)
    {
        string? scoped = string.IsNullOrWhiteSpace(siteCode) ? null : siteCode;
        IReadOnlyList<RecommendationSnapshot> recs = await energy.RecommendOptimizationsAsync(scoped, cancellationToken);
        return JsonSerializer.Serialize(recs, JsonOpts);
    }
}
