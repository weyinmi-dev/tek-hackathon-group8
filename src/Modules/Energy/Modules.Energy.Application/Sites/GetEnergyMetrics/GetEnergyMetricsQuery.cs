using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;

namespace Modules.Energy.Application.Sites.GetEnergyMetrics;

/// <summary>
/// Energy-side analytics for the Operations Dashboard. Mirrors the shape of the
/// Analytics module's MetricsResponse so the dashboard can render an "Energy" panel
/// alongside the "Ops" panel — regional health, source mix, anomaly type breakdown,
/// OPEX trend, and the top diesel-burn sites. Computed live from the Energy DbContext
/// (no time-series store), so the figures track real fleet state.
/// </summary>
public sealed record GetEnergyMetricsQuery() : IQuery<EnergyMetricsResponse>, ICachedQuery
{
    public string CacheKey => "energy:metrics";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(10);
}

public sealed record EnergyMetricsResponse(
    IReadOnlyList<EnergyRegionHealth> Regions,
    IReadOnlyList<EnergyMixSlice> EnergyMix,
    IReadOnlyList<EnergyAnomalyTypeBreakdown> AnomalyTypes,
    IReadOnlyList<double> OpexTrend,
    IReadOnlyList<TopDieselBurner> TopBurners,
    int OpenAnomalies,
    int CriticalSites,
    double FleetUptimePct,
    double AvgBatteryPct,
    long DailyOpexNgn);

/// <summary>Per-region health roll-up for the energy fleet. Tone is derived from
/// the share of critical/degraded sites — kept consistent with the ops-side
/// RegionHealthMetric tone semantics so the UI can reuse one renderer.</summary>
public sealed record EnergyRegionHealth(
    string Name,
    int Sites,
    int Critical,
    int Degraded,
    int Ok,
    int AvgUptimePct,
    int AvgBattPct,
    string Tone);

public sealed record EnergyMixSlice(string Source, int Pct);

public sealed record EnergyAnomalyTypeBreakdown(string Kind, int Count);

public sealed record TopDieselBurner(string SiteCode, string Name, string Region, int DailyDieselLitres, long DailyCostNgn);
