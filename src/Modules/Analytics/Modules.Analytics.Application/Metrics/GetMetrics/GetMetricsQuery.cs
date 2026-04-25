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
    IReadOnlyList<IncidentTypeBreakdown> IncidentTypes);

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
