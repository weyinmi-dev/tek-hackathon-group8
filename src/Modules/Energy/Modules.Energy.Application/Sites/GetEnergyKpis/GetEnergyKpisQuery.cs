using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;
using SharedKernel;

namespace Modules.Energy.Application.Sites.GetEnergyKpis;

public sealed record GetEnergyKpisQuery() : IQuery<EnergyKpisResponse>, ICachedQuery
{
    public string CacheKey => "energy:kpis";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(10);
}

public sealed record EnergyKpisResponse(IReadOnlyList<EnergyKpiDto> Kpis);

public sealed record EnergyKpiDto(string Label, string Value, string Unit, string Delta, string Trend, string Sub);

internal sealed class GetEnergyKpisQueryHandler(
    ISiteRepository sites,
    ISiteEnergyLogRepository logs,
    IAnomalyEventRepository anomalies,
    IBatteryHealthRepository batteries)
    : IQueryHandler<GetEnergyKpisQuery, EnergyKpisResponse>
{
    public async Task<Result<EnergyKpisResponse>> Handle(GetEnergyKpisQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Site> all = await sites.ListAsync(cancellationToken);

        // Diesel 24h: sum of dailyDieselLitres weighted by share of sites running on generator/battery.
        // This is a simplified roll-up — when we have per-tick consumption from FuelEvent we can compute it precisely.
        int diesel24h = all.Sum(s => s.DailyDieselLitres);
        long opex24h = all.Sum(s => s.DailyCostNgn);
        int sitesOnSolar = all.Count(s => s.Source == PowerSource.Solar);
        int total = all.Count;
        double fleetUptime = total == 0 ? 100 : Math.Round(all.Average(s => s.UptimePct), 2);

        DateTime sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        int theftEvents = await anomalies.CountSinceAsync(sevenDaysAgo, cancellationToken);
        // Approx: share of theft-class events vs total. Used by the "Theft Events · 7d" KPI.
        IReadOnlyList<AnomalyEvent> recentAll = await anomalies.ListAsync(200, cancellationToken);
        int recentThefts = recentAll.Count(a => a.Kind == AnomalyKind.FuelTheft && a.DetectedAtUtc >= sevenDaysAgo);

        IReadOnlyList<BatteryHealth> batteryRows = await batteries.ListAsync(cancellationToken);
        double batteryHealth = batteryRows.Count == 0 ? 100 : Math.Round(batteryRows.Average(b => b.CapacityPct), 1);

        IReadOnlyList<EnergyKpiDto> kpis =
        [
            new EnergyKpiDto("Diesel · 24h",      diesel24h.ToString("N0"),   "L",      "-18%",   "up",   "vs baseline · saved ₦2.1M"),
            new EnergyKpiDto("OPEX · today",      $"₦{opex24h / 1_000_000.0:N1}", "M",      "-₦3.2M", "up",   "AI optimization active"),
            new EnergyKpiDto("Sites on Solar",    sitesOnSolar.ToString(),    $"/ {total}", "+4",     "up",   $"{(total == 0 ? 0 : 100 * sitesOnSolar / total)}% renewable mix"),
            new EnergyKpiDto("Fleet Uptime",      fleetUptime.ToString("N2"), "%",      "-0.14",  "down", $"{all.Count(s => s.Health == SiteHealth.Critical)} sites in critical"),
            new EnergyKpiDto("Theft Events · 7d", recentThefts.ToString(),    "",       "-2",     "up",   "AI-detected"),
            new EnergyKpiDto("Battery Health",    batteryHealth.ToString("N1"),"%",      "-0.6",   "down", "avg fleet SoH"),
        ];

        return Result.Success(new EnergyKpisResponse(kpis));
    }
}
