using Modules.Alerts.Application.Alerts.GetAlerts;
using Modules.Energy.Application.Anomalies.GetAnomalies;
using Modules.Energy.Application.Sites.GetSites;

namespace Web.Api.Endpoints.Geo;

/// <summary>
/// Wire-format wrappers that piggyback an OSM <see cref="GeoSummary"/> onto the
/// existing module DTOs without modifying the module API surfaces. Each record
/// preserves the original DTO's field set 1:1 so existing frontend types stay
/// compatible — only the new optional <c>Geo</c> field is added.
///
/// We define wrappers (rather than mutating the module DTO records) to keep
/// the modular-monolith boundary intact: the AI module knows about OSM, the
/// Alerts and Energy modules don't have to.
/// </summary>
public sealed record AlertWithGeo(
    string Id,
    string Sev,
    string Status,
    string Title,
    string Region,
    string Tower,
    string Cause,
    int Users,
    double Confidence,
    string Time,
    string? AssignedTeam,
    string? DispatchTarget,
    GeoSummary? Geo)
{
    public static AlertWithGeo From(AlertDto dto, GeoSummary? geo) => new(
        dto.Id, dto.Sev, dto.Status, dto.Title, dto.Region, dto.Tower, dto.Cause,
        dto.Users, dto.Confidence, dto.Time, dto.AssignedTeam, dto.DispatchTarget, geo);
}

public sealed record SiteWithGeo(
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
    string? Anomaly,
    GeoSummary? Geo)
{
    public static SiteWithGeo From(SiteDto dto, GeoSummary? geo) => new(
        dto.Id, dto.Name, dto.Region, dto.Source, dto.BattPct, dto.DieselPct,
        dto.SolarKw, dto.GridUp, dto.DailyDieselLitres, dto.CostNgn, dto.UptimePct,
        dto.Solar, dto.Health, dto.Anomaly, geo);
}

public sealed record SitesWithGeoResponse(IReadOnlyList<SiteWithGeo> Sites);

public sealed record AnomalyWithGeo(
    Guid Id,
    string Site,
    string Kind,
    string Sev,
    string T,
    string Detail,
    double Conf,
    string Model,
    bool Acknowledged,
    GeoSummary? Geo)
{
    public static AnomalyWithGeo From(AnomalyDto dto, GeoSummary? geo) => new(
        dto.Id, dto.Site, dto.Kind, dto.Sev, dto.T, dto.Detail, dto.Conf, dto.Model,
        dto.Acknowledged, geo);
}

public sealed record AnomaliesWithGeoResponse(IReadOnlyList<AnomalyWithGeo> Anomalies);
