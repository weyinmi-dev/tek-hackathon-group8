namespace Modules.Energy.Api;

/// <summary>
/// In-process contract other modules (notably Ai's MCP plugin and SK skills) consume to read
/// energy state. Never goes over HTTP — stays in the same process per the modular-monolith rule.
/// </summary>
public interface IEnergyApi
{
    Task<IReadOnlyList<SiteSnapshot>> ListSitesAsync(CancellationToken ct = default);
    Task<SiteSnapshot?> GetSiteAsync(string siteCode, CancellationToken ct = default);
    Task<IReadOnlyList<AnomalySnapshot>> ListAnomaliesAsync(int take, CancellationToken ct = default);
    Task<EnergyKpiSnapshot> GetKpisAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DieselTracePoint>> GetSiteDieselTraceAsync(string siteCode, int hours, CancellationToken ct = default);
    Task<IReadOnlyList<RecommendationSnapshot>> RecommendOptimizationsAsync(string? siteCode, CancellationToken ct = default);
}

public sealed record SiteSnapshot(
    string Code,
    string Name,
    string Region,
    string Source,
    int BattPct,
    int DieselPct,
    double SolarKw,
    bool GridUp,
    int DailyDieselLitres,
    long DailyCostNgn,
    double UptimePct,
    bool HasSolar,
    string Health,
    string? AnomalyNote);

public sealed record AnomalySnapshot(
    Guid Id,
    string SiteCode,
    string Kind,
    string Severity,
    string Detail,
    double Confidence,
    string Model,
    DateTime DetectedAtUtc,
    bool Acknowledged);

public sealed record EnergyKpiSnapshot(
    int Diesel24hLitres,
    long Opex24hNgn,
    int SitesOnSolar,
    int TotalSites,
    double FleetUptimePct,
    int TheftEvents7d,
    double BatteryHealthPct);

public sealed record DieselTracePoint(DateTime At, int DieselPct, int LitresDelta);

public sealed record RecommendationSnapshot(
    string Title,
    string Detail,
    string Tone,
    long EstimatedDailySavingsNgn);
