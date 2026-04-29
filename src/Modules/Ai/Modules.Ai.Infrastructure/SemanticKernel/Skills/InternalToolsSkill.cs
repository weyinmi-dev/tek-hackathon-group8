using System.ComponentModel;
using System.Text.Json;
using MediatR;
using Modules.Ai.Application.Tools.AnalyzeLatency;
using Modules.Ai.Application.Tools.FindBestConnectivity;
using Modules.Ai.Application.Tools.GetNetworkMetrics;
using Modules.Ai.Application.Tools.GetOutages;
using Modules.Ai.Application.Tools.Models;
using SharedKernel;
using Microsoft.SemanticKernel;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill that surfaces the modular-monolith's <c>/AI/Tools</c> layer to the LLM.
/// Each kernel function is a thin shim over a MediatR query — so every tool call
/// rides the same pipeline (logging, validation, exception handling) as a regular
/// application use case. No HTTP, no out-of-process boundaries — pure in-process
/// dispatch.
/// </summary>
public sealed class InternalToolsSkill(ISender sender)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    [KernelFunction("get_network_metrics")]
    [Description("Tool: get_network_metrics. Returns the current signal/load/status snapshot for every tower in a region. Use it when you need fresh network state, not historical context.")]
    public Task<string> GetNetworkMetricsAsync(
        [Description("Region name, e.g. 'Lagos West', 'Lekki'. Pass empty string for all regions.")] string region,
        CancellationToken cancellationToken = default)
        => DispatchAsync(new GetNetworkMetricsToolQuery(region ?? ""), cancellationToken);

    [KernelFunction("get_outages")]
    [Description("Tool: get_outages. Returns active or recent incidents, optionally filtered to a region.")]
    public Task<string> GetOutagesAsync(
        [Description("Region name; pass empty string for the whole metro.")] string region = "",
        [Description("Set true to include resolved/monitoring incidents in the result. Default: active-only.")] bool includeAllStatuses = false,
        CancellationToken cancellationToken = default)
        => DispatchAsync(new GetOutagesToolQuery(string.IsNullOrWhiteSpace(region) ? null : region, includeAllStatuses), cancellationToken);

    [KernelFunction("analyze_latency")]
    [Description("Tool: analyze_latency. Aggregates a region's tower telemetry into a one-line diagnosis (signal/load/congestion/offline counts) and a suggested next step.")]
    public Task<string> AnalyzeLatencyAsync(
        [Description("Region name to analyse, e.g. 'Lekki' or 'Lagos West'.")] string region,
        CancellationToken cancellationToken = default)
        => DispatchAsync(new AnalyzeLatencyToolQuery(region), cancellationToken);

    [KernelFunction("find_best_connectivity")]
    [Description("Tool: find_best_connectivity. Ranks healthy towers in a region by signal × capacity headroom — useful for choosing failover / load-shed targets.")]
    public Task<string> FindBestConnectivityAsync(
        [Description("Region name; empty string considers the whole metro.")] string region = "",
        [Description("Maximum number of recommendations to return (1-10).")] int limit = 3,
        CancellationToken cancellationToken = default)
        => DispatchAsync(new FindBestConnectivityToolQuery(region ?? "", limit), cancellationToken);

    private async Task<string> DispatchAsync<TResult>(IRequest<Result<TResult>> query, CancellationToken cancellationToken)
        where TResult : notnull
    {
        Result<TResult> result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return JsonSerializer.Serialize(new { error = result.Error.Code, message = result.Error.Description }, JsonOpts);
        }
        return JsonSerializer.Serialize(result.Value, JsonOpts);
    }
}
