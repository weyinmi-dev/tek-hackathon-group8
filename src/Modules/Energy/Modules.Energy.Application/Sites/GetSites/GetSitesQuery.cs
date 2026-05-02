using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;

namespace Modules.Energy.Application.Sites.GetSites;

public sealed record GetSitesQuery() : IQuery<SitesResponse>, ICachedQuery
{
    // Short cache: ticker mutates state every 30s, but read-heavy dashboards can tolerate
    // a few seconds of staleness, and burst refreshes from multiple browser tabs would
    // otherwise hammer EF.
    public string CacheKey => "energy:sites";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(5);
}

public sealed record SitesResponse(IReadOnlyList<SiteDto> Sites);

public sealed record SiteDto(
    string Id,
    string Name,
    string Region,
    string Source,
    int BattPct,
    int DieselPct,
    double SolarKw,
    bool GridUp,
    int DailyDieselLitres,
    long CostNgn,
    double UptimePct,
    bool Solar,
    string Health,
    string? Anomaly);
