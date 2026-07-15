using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Modules.Network.Domain.Assets;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Maintenance;
using Modules.Network.Domain.Towers;
using SharedKernel;

namespace Modules.Network.Application.Sites;

/// <summary>
/// Everything the Site Details page renders, assembled from the synchronised state — not from the
/// raw upload. The tower, equipment and tickets come from the aggregates the pipeline maintains; the
/// environmental and performance readings come from the latest stored snapshot, because those are
/// point-in-time measurements with no aggregate of their own.
///
/// This is deliberately a read model built at query time rather than a materialised view: the site
/// count is in the hundreds, the page is opened one site at a time, and a stale projection would
/// contradict the "always shows the latest snapshot" requirement.
/// </summary>
public sealed record GetSiteDetailQuery(string SiteCode) : IQuery<SiteDetail>;

public sealed record SiteDetail(
    string SiteCode,
    string Name,
    string Region,
    string StatusWire,
    int SignalPct,
    int LoadPct,
    string? Issue,
    double Latitude,
    double Longitude,
    DateTime UpdatedAtUtc,

    // Provenance — null when this site has never received a snapshot (a seeded tower, say).
    string? Provider,
    string? Environment,
    string? Vendor,
    string? SiteId,
    IReadOnlyList<string> Technologies,
    int? HealthScore,
    DateTimeOffset? LastSynchronisedAt,
    DateTimeOffset? LastHeartbeat,
    int? SnapshotVersion,

    SnapshotEnvironmentalMetrics? Environmental,
    SnapshotPerformanceMetrics? Performance,
    IReadOnlyList<SnapshotAlarm> ActiveAlarms,

    IReadOnlyList<SiteEquipmentDto> Equipment,
    IReadOnlyList<MaintenanceTicketDto> Tickets,
    DateOnly? LastMaintenanceDate,
    DateOnly? NextScheduledMaintenance);

public sealed record SiteEquipmentDto(
    string EquipmentId, string Type, string? Model, string? Status,
    bool IsActive, DateTime LastSeenAtUtc, DateTime? RetiredAtUtc);

public sealed record MaintenanceTicketDto(
    string TicketId, string Status, string? Priority, string? Issue,
    string? EngineerId, string? EngineerName,
    DateTimeOffset? CreatedAt, DateTimeOffset? EstimatedArrival,
    DateTimeOffset? CompletedAt, string? CompletedAction);

internal sealed class GetSiteDetailQueryHandler(
    ITowerRepository towers,
    IIngestionRunRepository runs,
    ISiteEquipmentRepository equipment,
    IMaintenanceTicketRepository tickets)
    : IQueryHandler<GetSiteDetailQuery, SiteDetail>
{
    public async Task<Result<SiteDetail>> Handle(GetSiteDetailQuery request, CancellationToken cancellationToken)
    {
        string code = request.SiteCode.Trim().ToUpperInvariant();

        Tower? tower = await towers.GetByCodeAsync(code, cancellationToken);
        if (tower is null)
        {
            return Result.Failure<SiteDetail>(Error.NotFound(
                "Network.Site.NotFound", $"Site {code} not found."));
        }

        SiteSnapshotRecord? latest = await runs.GetLatestSnapshotForSiteAsync(code, cancellationToken);
        SiteSnapshotPayload? snapshot = latest is null ? null : SiteSnapshotPayload.Deserialize(latest.RawJson);

        IReadOnlyList<SiteEquipment> units = await equipment.ListForSiteAsync(code, cancellationToken);
        IReadOnlyList<MaintenanceTicket> jobs = await tickets.ListForSiteAsync(code, cancellationToken);

        return Result.Success(new SiteDetail(
            SiteCode: tower.Code,
            Name: tower.Name,
            Region: tower.Region,
            StatusWire: tower.Status.ToString().ToUpperInvariant(),
            SignalPct: tower.SignalPct,
            LoadPct: tower.LoadPct,
            Issue: tower.Issue,
            Latitude: tower.Latitude,
            Longitude: tower.Longitude,
            UpdatedAtUtc: tower.UpdatedAtUtc,

            Provider: latest?.Provider,
            Environment: latest?.Environment,
            Vendor: latest?.Vendor,
            SiteId: latest?.SiteId,
            Technologies: string.IsNullOrWhiteSpace(latest?.Technologies)
                ? []
                : latest!.Technologies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            HealthScore: latest?.HealthScore,

            // "Last synchronised" is when we ingested it; "last heartbeat" is when the site last
            // spoke to the OSS. They answer different questions and a stale one of either is a
            // different kind of problem, so both are surfaced.
            LastSynchronisedAt: latest?.CapturedAt ?? latest?.GeneratedAt,
            LastHeartbeat: latest?.LastHeartbeat,
            SnapshotVersion: latest?.SnapshotVersion,

            Environmental: snapshot?.Environmental,
            Performance: snapshot?.Performance,
            ActiveAlarms: snapshot?.ActiveAlarms.Where(SnapshotDerivations.IsOpen).ToList() ?? [],

            Equipment: units
                .OrderByDescending(e => e.IsActive)
                .ThenBy(e => e.EquipmentId, StringComparer.Ordinal)
                .Select(e => new SiteEquipmentDto(
                    e.EquipmentId, e.Type, e.Model, e.Status, e.IsActive, e.LastSeenAtUtc, e.RetiredAtUtc))
                .ToList(),

            Tickets: jobs
                .OrderBy(t => t.Status)
                .ThenByDescending(t => t.CreatedAt)
                .Select(t => new MaintenanceTicketDto(
                    t.TicketId, t.Status.ToString(), t.Priority, t.Issue,
                    t.AssignedEngineerId, t.AssignedEngineerName,
                    t.CreatedAt, t.EstimatedArrival, t.CompletedAt, t.CompletedAction))
                .ToList(),

            LastMaintenanceDate: snapshot?.Maintenance?.LastMaintenanceDate,
            NextScheduledMaintenance: snapshot?.Maintenance?.NextScheduledMaintenance));
    }
}
