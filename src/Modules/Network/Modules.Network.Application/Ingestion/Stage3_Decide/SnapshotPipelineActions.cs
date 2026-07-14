namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// The synchronisation half of the <see cref="PipelineAction"/> union — the actions planned from a
/// full OSS site snapshot rather than from AI analysis.
///
/// They sit in the same union, are planned in the same stage, and are executed in the same stage as
/// the AI-derived ones. That is the whole point: a snapshot upload is not a second pipeline, it is
/// more actions flowing through the one that already exists.
///
/// These carry no <c>AnomalyFingerprint</c> except where they touch alerts, because most of them
/// aren't anomalies — they are reported facts about a site's assets and state.
/// </summary>
public sealed record UpsertTowerAction(
    string TowerCode,
    string Name,
    string Region,
    double? Latitude,
    double? Longitude,
    int? SignalPct,
    int? LoadPct,
    string StatusWire,
    string? Issue) : PipelineAction(AnomalyFingerprint: string.Empty);

/// <summary>
/// An alarm the provider is currently reporting. Distinct from <see cref="CreateAlertAction"/> /
/// <see cref="UpdateAlertAction"/> — those carry a <c>DetectedAnomaly</c>, which is something our
/// analyzer *inferred*. An OSS alarm is something the network *stated*, so it gets its own action
/// rather than being dressed up as a detection. Both converge on the same alert executor.
/// </summary>
public sealed record SyncAlarmAction(
    string AnomalyFingerprint,
    Guid? ExistingAlertId,
    string SeverityWire,
    string TowerCode,
    string Region,
    string Title,
    string Cause,
    DateTime RaisedAtUtc) : PipelineAction(AnomalyFingerprint);

/// <summary>
/// An alarm that was live here but is absent from the site's latest snapshot — the fault cleared
/// upstream, so the alert must stop showing as open.
/// </summary>
public sealed record ResolveAlarmAction(
    string AnomalyFingerprint,
    string Reason) : PipelineAction(AnomalyFingerprint);

/// <summary>
/// The complete equipment inventory the snapshot reports for a site. The whole list travels
/// together — not one action per unit — because the executor needs to know what was *absent* in
/// order to retire it, and absence is only meaningful against the full reported set.
/// </summary>
public sealed record SyncEquipmentAction(
    string SiteCode,
    IReadOnlyList<EquipmentReport> Reported,
    DateTime ObservedAtUtc) : PipelineAction(AnomalyFingerprint: string.Empty);

public sealed record EquipmentReport(string EquipmentId, string Type, string? Model, string? Status);

/// <summary>
/// The site's maintenance picture: what is open, and what was completed. Travels as one action for
/// the same reason as equipment — a ticket that appears in neither list has dropped out of the feed
/// and must be archived, which can only be determined from the complete picture.
/// </summary>
public sealed record SyncMaintenanceAction(
    string SiteCode,
    IReadOnlyList<TicketReport> OpenTickets,
    IReadOnlyList<CompletedWorkReport> CompletedWork,
    DateTime ObservedAtUtc) : PipelineAction(AnomalyFingerprint: string.Empty);

public sealed record TicketReport(
    string TicketId,
    string? Priority,
    string? ProviderStatus,
    string? Issue,
    string? EngineerId,
    string? EngineerName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? EstimatedArrival);

public sealed record CompletedWorkReport(
    string TicketId,
    DateTimeOffset? CompletedAt,
    string? EngineerName,
    string? Action);

/// <summary>
/// The site's energy plant as the snapshot reports it, plus the telemetry row to append. Executed
/// by the Energy module through <c>IEnergySyncExecutor</c>, mirroring how alerts are executed by
/// the Alerts module through <c>IAlertActionExecutor</c>.
/// </summary>
public sealed record SyncEnergySiteAction(
    string SiteCode,
    string Name,
    string Region,
    int? BatteryPct,
    int? DieselPct,
    bool GridUp,
    string SourceWire,
    bool HasOpenAlarm,
    string? AnomalyNote,
    DateTime ObservedAtUtc) : PipelineAction(AnomalyFingerprint: string.Empty);
