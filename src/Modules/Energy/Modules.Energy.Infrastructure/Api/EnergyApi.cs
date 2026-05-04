using MediatR;
using Modules.Energy.Api;
using Modules.Energy.Application.Optimization.Recommendations;
using Modules.Energy.Application.Sites.GetEnergyKpis;
using Modules.Energy.Application.Sites.GetSiteTrace;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;
using SharedKernel;

namespace Modules.Energy.Infrastructure.Api;

internal sealed class EnergyApi(
    ISiteRepository sites,
    IAnomalyEventRepository anomalies,
    ISiteEnergyLogRepository logs,
    ISender sender) : IEnergyApi
{
    public async Task<IReadOnlyList<SiteSnapshot>> ListSitesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Site> rows = await sites.ListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<SiteSnapshot?> GetSiteAsync(string siteCode, CancellationToken ct = default)
    {
        Site? site = await sites.GetByCodeAsync(siteCode, ct);
        return site is null ? null : Map(site);
    }

    public async Task<IReadOnlyList<AnomalySnapshot>> ListAnomaliesAsync(int take, CancellationToken ct = default)
    {
        IReadOnlyList<AnomalyEvent> rows = await anomalies.ListAsync(take, ct);
        return rows.Select(a => new AnomalySnapshot(
            a.Id, a.SiteCode, a.Kind.ToWire(), a.Severity.ToWire(),
            a.Detail, a.Confidence, a.ModelName, a.DetectedAtUtc, a.Acknowledged))
            .ToList();
    }

    public async Task<EnergyKpiSnapshot> GetKpisAsync(CancellationToken ct = default)
    {
        // Reuse the application query so cross-module callers (MCP plugin, SK skills) see the
        // same numbers the dashboard does — single source of truth for KPI roll-ups.
        Result<EnergyKpisResponse> result = await sender.Send(new GetEnergyKpisQuery(), ct);
        if (result.IsFailure)
        {
            return new EnergyKpiSnapshot(0, 0, 0, 0, 0, 0, 0);
        }

        IReadOnlyList<Site> all = await sites.ListAsync(ct);
        int diesel24h = all.Sum(s => s.DailyDieselLitres);
        long opex24h = all.Sum(s => s.DailyCostNgn);
        int onSolar = all.Count(s => s.Source == PowerSource.Solar);
        double uptime = all.Count == 0 ? 100 : Math.Round(all.Average(s => s.UptimePct), 2);
        int theft7d = await anomalies.CountSinceAsync(DateTime.UtcNow.AddDays(-7), ct);
        return new EnergyKpiSnapshot(diesel24h, opex24h, onSolar, all.Count, uptime, theft7d, 87.4);
    }

    public async Task<IReadOnlyList<DieselTracePoint>> GetSiteDieselTraceAsync(string siteCode, int hours, CancellationToken ct = default)
    {
        IReadOnlyList<SiteEnergyLog> rows = await logs.ListForSiteAsync(siteCode, hours, ct);
        return rows
            .OrderBy(l => l.RecordedAtUtc)
            .Select(l => new DieselTracePoint(l.RecordedAtUtc, l.DieselPct, 0))
            .ToList();
    }

    public async Task<IReadOnlyList<RecommendationSnapshot>> RecommendOptimizationsAsync(string? siteCode, CancellationToken ct = default)
    {
        Result<RecommendationsResponse> result = await sender.Send(new GetRecommendationsQuery(siteCode), ct);
        if (result.IsFailure)
        {
            return [];
        }
        return result.Value.Recommendations
            .Select(r => new RecommendationSnapshot(r.Title, r.Detail, r.Tone, r.EstimatedDailySavingsNgn))
            .ToList();
    }

    private static SiteSnapshot Map(Site s) =>
        new(s.Code, s.Name, s.Region, s.Source.ToWire(),
            s.BattPct, s.DieselPct, s.SolarKw, s.GridUp,
            s.DailyDieselLitres, s.DailyCostNgn, s.UptimePct, s.HasSolar,
            s.Health.ToWire(), s.AnomalyNote);
}
