using Application.Abstractions.Pipeline;
using MediatR;
using Modules.Network.Api;
using Modules.Network.Application.Ingestion.Pipeline;
using Modules.Network.Application.Ingestion.Queries;
using Modules.Network.Application.Sites;
using Modules.Network.Domain.Towers;
using SharedKernel;

namespace Modules.Network.Infrastructure.Api;

/// <summary>
/// The cross-module read port for Network.
///
/// The tower methods read straight from the repository. The synchronisation methods dispatch the
/// same MediatR queries the HTTP API uses and map the results onto the flat <c>.Api</c> contract —
/// so the Copilot and the UI can never disagree about a site's state, and none of the assembly logic
/// is written twice. Mapping to a separate shape here is the price of the module boundary: the Ai
/// module may not reference Network.Application, and the architecture tests enforce it.
/// </summary>
internal sealed class NetworkApi(
    ITowerRepository towers,
    ISender sender,
    SnapshotCalibrationOptions calibration) : INetworkApi
{
    public async Task<IReadOnlyList<TowerSnapshot>> ListTowersAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Tower> all = await towers.ListAsync(ct);
        return all.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<TowerSnapshot>> ListByRegionAsync(string region, CancellationToken ct = default)
    {
        IReadOnlyList<Tower> rows = await towers.ListByRegionAsync(region, ct);
        return rows.Select(Map).ToList();
    }

    public async Task<TowerSnapshot?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        Tower? t = await towers.GetByCodeAsync(code, ct);
        return t is null ? null : Map(t);
    }

    public async Task<IReadOnlyList<RegionHealth>> GetRegionHealthAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Tower> all = await towers.ListAsync(ct);
        return all.GroupBy(t => t.Region)
            .Select(g => new RegionHealth(
                g.Key,
                g.Count(),
                g.Count(t => t.Status == TowerStatus.Critical),
                g.Count(t => t.Status == TowerStatus.Warn),
                (int)Math.Round(g.Average(t => t.SignalPct))))
            .OrderBy(r => r.Region)
            .ToList();
    }

    // ── Synchronised OSS snapshot state ───────────────────────────────────────

    public async Task<SiteSyncState?> GetSiteSyncStateAsync(string siteCode, CancellationToken ct = default)
    {
        Result<SiteDetail> result = await sender.Send(new GetSiteDetailQuery(siteCode), ct);
        if (result.IsFailure)
        {
            return null;
        }

        SiteDetail d = result.Value;
        SnapshotEnvironmentalMetrics? env = d.Environmental;
        SnapshotPerformanceMetrics? perf = d.Performance;

        return new SiteSyncState(
            SiteCode: d.SiteCode,
            Name: d.Name,
            Region: d.Region,
            Status: d.StatusWire,
            SignalPct: d.SignalPct,
            LoadPct: d.LoadPct,
            Issue: d.Issue,
            Provider: d.Provider,
            Vendor: d.Vendor,
            Technologies: d.Technologies,
            HealthScore: d.HealthScore,
            LastSynchronisedAt: d.LastSynchronisedAt,
            LastHeartbeat: d.LastHeartbeat,
            TemperatureC: env?.Temperature,
            BatteryPct: SnapshotDerivations.BatteryPctFromVoltage(env?.BatteryVoltage, calibration),
            GeneratorFuelPercent: env?.GeneratorFuelPercent,
            GridUp: env?.MainPowerAvailable,
            GeneratorRunning: env?.GeneratorRunning,
            LatencyMs: perf?.LatencyMs,
            AvailabilityPercent: perf?.AvailabilityPercent,
            ConnectedUsers: perf?.ConnectedUsers,
            ActiveAlarms: d.ActiveAlarms
                .Select(a => new SiteAlarm(
                    a.AlarmId, a.Severity, a.Category, a.Type, a.Status, a.RaisedAt, a.Description))
                .ToList(),
            Equipment: d.Equipment
                .Select(e => new SiteEquipmentState(e.EquipmentId, e.Type, e.Model, e.Status, e.IsActive))
                .ToList(),
            OpenTickets: d.Tickets
                .Where(t => t.Status == "Open")
                .Select(t => new SiteTicket(
                    t.TicketId, t.Status, t.Priority, t.Issue, t.EngineerName, t.CreatedAt, t.EstimatedArrival))
                .ToList());
    }

    public async Task<IReadOnlyList<SiteTelemetrySample>> GetSiteTelemetryAsync(
        string siteCode, int hours, CancellationToken ct = default)
    {
        Result<SiteTelemetry> result = await sender.Send(new GetSiteTelemetryQuery(siteCode, hours), ct);
        if (result.IsFailure)
        {
            return [];
        }

        return result.Value.Points
            .Select(p => new SiteTelemetrySample(
                p.At, p.HealthScore, p.SignalPct, p.LoadPct, p.LatencyMs, p.TemperatureC,
                p.BatteryPct, p.DieselPct, p.GridUp, p.DownlinkTrafficGb, p.ConnectedUsers, p.OpenAlarmCount))
            .ToList();
    }

    public async Task<IReadOnlyList<SyncRunSummary>> ListSyncRunsAsync(
        string? siteCode, int take, CancellationToken ct = default)
    {
        Result<IngestionRunPage> result = await sender.Send(
            new ListIngestionRunsQuery(SiteCode: siteCode, Take: take <= 0 ? 10 : take), ct);

        return result.IsFailure ? [] : result.Value.Runs.Select(MapRun).ToList();
    }

    public async Task<SyncRunSummary?> GetSyncRunAsync(Guid ingestionRunId, CancellationToken ct = default)
    {
        Result<IngestionRunSummary> result = await sender.Send(new GetIngestionRunQuery(ingestionRunId), ct);
        return result.IsFailure ? null : MapRun(result.Value);
    }

    private static SyncRunSummary MapRun(IngestionRunSummary r) =>
        new(
            IngestionRunId: r.IngestionRunId,
            FileName: r.FileName ?? "unknown",
            Status: r.FinalStatus.ToString(),
            SubmittedBy: r.SubmittedBy ?? "unknown",
            StartedAt: r.StartedAt ?? default,
            CompletedAt: r.CompletedAt,
            DurationMs: r.DurationMs,
            RecordsCreated: r.RecordsCreated,
            RecordsUpdated: r.RecordsUpdated,
            RecordsArchived: r.RecordsArchived,
            AlertsCreated: r.AlertsCreated,
            AlertsUpdated: r.AlertsUpdated,
            OptimizationsCreated: r.OptimizationsCreated,
            Warnings: r.Warnings,
            FailureReason: r.FailureReason,
            SiteCodes: r.SyncedSites.Select(s => s.SiteCode).ToList(),
            Provider: r.SyncedSites.FirstOrDefault()?.Provider);

    private static TowerSnapshot Map(Tower t) =>
        new(t.Code, t.Name, t.Region, t.SignalPct, t.LoadPct, t.Status.ToWire(), t.Issue, t.Latitude, t.Longitude);
}
