namespace Modules.Ai.Application.Tools.Models;

// Lightweight MCP-style internal tool contracts. Each tool is exposed to Semantic
// Kernel as a [KernelFunction] but executed as an in-process MediatR query — no
// HTTP, no serialization round-trip across module boundaries.

public sealed record TowerMetric(
    string Code,
    string Name,
    string Region,
    int SignalPct,
    int LoadPct,
    string Status,
    string? Issue);

public sealed record GetNetworkMetricsResult(string Region, int TowerCount, IReadOnlyList<TowerMetric> Towers);

public sealed record OutageRow(
    string Code,
    string Severity,
    string Status,
    string Region,
    string TowerCode,
    string Cause,
    int SubscribersAffected,
    double Confidence);

public sealed record GetOutagesResult(int Count, IReadOnlyList<OutageRow> Outages);

public sealed record LatencyAnalysisResult(
    string Region,
    int TowerCount,
    int AvgSignalPct,
    int AvgLoadPct,
    int CongestedTowers,
    int OfflineTowers,
    string Diagnosis,
    string SuggestedNextStep);

public sealed record ConnectivityRecommendation(
    string TowerCode,
    string Name,
    string Region,
    int SignalPct,
    int LoadPct,
    string Status,
    string Reason);

public sealed record FindBestConnectivityResult(
    string Region,
    IReadOnlyList<ConnectivityRecommendation> Recommendations);
