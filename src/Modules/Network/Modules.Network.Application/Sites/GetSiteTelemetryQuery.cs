using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Sites;

/// <summary>
/// Historical telemetry for one site, over a time range. Backs every trend chart on the site page:
/// signal, traffic, latency, temperature, battery, KPIs and health score.
///
/// It is read from the stored snapshot history rather than from a purpose-built time-series table.
/// The snapshots are already an append-only, timestamped, indexed record of every state a site has
/// been reported in — a second store would be the same data written twice, with two chances to
/// disagree. Energy trends (diesel, battery, grid) additionally flow into the existing SiteEnergyLog,
/// so the pre-existing diesel trace and OPEX charts pick up snapshot data with no changes at all.
/// </summary>
public sealed record GetSiteTelemetryQuery(string SiteCode, int Hours = 24) : IQuery<SiteTelemetry>;

public sealed record SiteTelemetry(
    string SiteCode,
    int Hours,
    IReadOnlyList<SiteTelemetryPoint> Points);

/// <summary>
/// One reported state. Every metric is nullable because a feed may omit any of them, and a gap in a
/// series is information — plotting a missing temperature as zero would draw a cliff that never
/// happened.
/// </summary>
public sealed record SiteTelemetryPoint(
    DateTimeOffset At,
    int? HealthScore,
    int? SignalPct,
    int? LoadPct,
    int? LatencyMs,
    double? TemperatureC,
    double? HumidityPct,
    int? BatteryPct,
    int? DieselPct,
    bool? GridUp,
    double? DownlinkTrafficGb,
    double? UplinkTrafficGb,
    int? ConnectedUsers,
    double? AvailabilityPercent,
    double? PacketLossPercent,
    double? Rsrp,
    double? Sinr,
    double? PrbUtilization,
    int OpenAlarmCount);

internal sealed class GetSiteTelemetryQueryHandler(IIngestionRunRepository runs)
    : IQueryHandler<GetSiteTelemetryQuery, SiteTelemetry>
{
    /// <summary>
    /// A 15-minute feed produces 96 points a day. This caps a request at roughly a year of them, so
    /// a caller asking for an absurd range gets a bounded response instead of the whole table.
    /// </summary>
    private const int MaxPoints = 5000;

    public async Task<Result<SiteTelemetry>> Handle(
        GetSiteTelemetryQuery request, CancellationToken cancellationToken)
    {
        int hours = Math.Clamp(request.Hours, 1, 24 * 365);
        string code = request.SiteCode.Trim().ToUpperInvariant();
        DateTimeOffset since = DateTimeOffset.UtcNow.AddHours(-hours);

        IReadOnlyList<SiteSnapshotRecord> history =
            await runs.ListSnapshotsForSiteAsync(code, since, MaxPoints, cancellationToken);

        var points = new List<SiteTelemetryPoint>(history.Count);
        foreach (SiteSnapshotRecord record in history)
        {
            SiteSnapshotPayload? payload = SiteSnapshotPayload.Deserialize(record.RawJson);
            if (payload is null)
            {
                // A corrupt row must not take the whole chart down with it — skip the point and keep
                // the series. The run detail is where a bad document should surface, not here.
                continue;
            }

            points.Add(ToPoint(record, payload));
        }

        return Result.Success(new SiteTelemetry(code, hours, points));
    }

    private static SiteTelemetryPoint ToPoint(SiteSnapshotRecord record, SiteSnapshotPayload payload)
    {
        SnapshotPerformanceMetrics? perf = payload.Performance;
        SnapshotEnvironmentalMetrics? env = payload.Environmental;

        return new SiteTelemetryPoint(
            At: record.CapturedAt ?? record.GeneratedAt,
            HealthScore: payload.Site.HealthScore,
            SignalPct: SnapshotDerivations.SignalPctFromRsrp(SnapshotDerivations.Kpi(perf?.Kpis, "RSRP")),
            LoadPct: perf?.CellUtilizationPercent,
            LatencyMs: perf?.LatencyMs,
            TemperatureC: env?.Temperature,
            HumidityPct: env?.Humidity,
            BatteryPct: SnapshotDerivations.BatteryPctFromVoltage(env?.BatteryVoltage),
            DieselPct: env?.GeneratorFuelPercent,
            GridUp: env?.MainPowerAvailable,
            DownlinkTrafficGb: perf?.DownlinkTrafficGb,
            UplinkTrafficGb: perf?.UplinkTrafficGb,
            ConnectedUsers: perf?.ConnectedUsers,
            AvailabilityPercent: perf?.AvailabilityPercent,
            PacketLossPercent: perf?.PacketLossPercent,
            Rsrp: SnapshotDerivations.Kpi(perf?.Kpis, "RSRP"),
            Sinr: SnapshotDerivations.Kpi(perf?.Kpis, "SINR"),
            PrbUtilization: SnapshotDerivations.Kpi(perf?.Kpis, "PRB Utilization"),
            OpenAlarmCount: payload.ActiveAlarms.Count(SnapshotDerivations.IsOpen));
    }
}
