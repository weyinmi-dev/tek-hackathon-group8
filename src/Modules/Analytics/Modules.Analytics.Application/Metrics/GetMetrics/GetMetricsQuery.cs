using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;

namespace Modules.Analytics.Application.Metrics.GetMetrics;

public sealed record GetMetricsQuery() : IQuery<MetricsResponse>, ICachedQuery
{
    public string CacheKey => "metrics:lagos";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(10);
}

public sealed record MetricsResponse(
    IReadOnlyList<KpiCard> Kpis,
    SparkSeries Sparks,
    IReadOnlyList<RegionHealthMetric> Regions,
    IReadOnlyList<IncidentTypeBreakdown> IncidentTypes,
    IReadOnlyList<RegionLatencySeries> RegionLatency,
    IReadOnlyList<TopCopilotQuery> TopQueries);

public sealed record KpiCard(
    string Label,
    string Value,
    string Unit,
    string Delta,
    string Trend,
    string Sub);

public sealed record SparkSeries(
    IReadOnlyList<double> Uptime,
    IReadOnlyList<double> Latency,
    IReadOnlyList<double> Incident,
    IReadOnlyList<double> Towers,
    IReadOnlyList<double> Subs,
    IReadOnlyList<double> Queries);

public sealed record RegionHealthMetric(string Name, int AvgSignal, string Tone);

public sealed record IncidentTypeBreakdown(string Type, int Count);

/// <summary>One latency curve per region for the BigChart on the Insights page.
/// Series is shaped as a 16-point trace ending at the region's current load — the curve is
/// derived from current state since we don't have a time-series store yet.</summary>
public sealed record RegionLatencySeries(string Name, string Color, IReadOnlyList<int> Series);

/// <summary>Top copilot queries grouped by Target text — pulled from audit entries
/// (Action == "copilot.query"). Drives the "Top Copilot Queries" card on Insights.</summary>
public sealed record TopCopilotQuery(string Query, int Count);
