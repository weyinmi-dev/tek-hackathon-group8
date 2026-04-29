using Application.Abstractions.Messaging;
using Modules.Ai.Application.Tools.Models;
using Modules.Network.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools.AnalyzeLatency;

/// <summary>
/// Tool: AnalyzeLatency. Surveys towers in a region and synthesizes a
/// one-line diagnosis based on aggregate signal/load/status — useful when
/// the LLM wants a deterministic numeric summary instead of raw rows.
/// </summary>
public sealed record AnalyzeLatencyToolQuery(string Region) : IQuery<LatencyAnalysisResult>;

internal sealed class AnalyzeLatencyToolHandler(INetworkApi network)
    : IQueryHandler<AnalyzeLatencyToolQuery, LatencyAnalysisResult>
{
    public async Task<Result<LatencyAnalysisResult>> Handle(AnalyzeLatencyToolQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TowerSnapshot> towers = await network.ListByRegionAsync(request.Region, cancellationToken);

        if (towers.Count == 0)
        {
            return Result.Success(new LatencyAnalysisResult(
                Region: request.Region,
                TowerCount: 0,
                AvgSignalPct: 0,
                AvgLoadPct: 0,
                CongestedTowers: 0,
                OfflineTowers: 0,
                Diagnosis: $"No towers found in region '{request.Region}'.",
                SuggestedNextStep: "Verify the region name — try 'Lagos West', 'Ikeja', 'Lekki', 'Festac'."));
        }

        int avgSignal = (int)Math.Round(towers.Average(t => t.SignalPct));
        int avgLoad = (int)Math.Round(towers.Average(t => t.LoadPct));
        int congested = towers.Count(t => t.LoadPct >= 85);
        int offline = towers.Count(t => string.Equals(t.Status, "offline", StringComparison.OrdinalIgnoreCase));

        (string diagnosis, string nextStep) = (avgSignal, congested, offline) switch
        {
            ( < 70, _, > 0) => (
                "Signal is suppressed and at least one tower is offline — consistent with backhaul or power impact.",
                "Run GetOutages for the region; correlate offline towers with active incidents."),
            (_, > 1, _) => (
                "Multiple towers above 85% load — region is congested.",
                "Recommend automated load-shed onto idle neighbouring cells, then capacity ticket."),
            ( < 75, _, _) => (
                "Signal averages below 75% — likely RF degradation or microwave jitter.",
                "Inspect microwave links and weather correlation for the affected sector."),
            _ => (
                "Latency profile is within nominal SLA bounds.",
                "Continue passive monitoring; no operator action required."),
        };

        return Result.Success(new LatencyAnalysisResult(
            request.Region, towers.Count, avgSignal, avgLoad, congested, offline, diagnosis, nextStep));
    }
}
