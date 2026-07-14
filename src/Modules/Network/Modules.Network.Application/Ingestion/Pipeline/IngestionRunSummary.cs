using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Application.Ingestion.Pipeline;

/// <summary>
/// What one upload did. Returned straight to the caller of <c>POST /network/ingest</c>, which is
/// what the upload UI renders as its synchronisation report, and re-read from the stored run by the
/// sync-history view.
/// </summary>
public sealed record IngestionRunSummary(
    Guid IngestionRunId,
    string ContentHash,
    IngestionStatus FinalStatus,
    int EventsParsed,
    int AnomaliesDetected,
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    bool TopologyChanged,
    bool DeduplicatedFromPriorRun,
    IReadOnlyList<StageTiming> StageTimings,
    string? FailureReason,

    // ── Synchronisation report ────────────────────────────────────────────────
    int RecordsCreated = 0,
    int RecordsUpdated = 0,
    int RecordsArchived = 0,
    int TelemetryRowsAppended = 0,
    IReadOnlyList<string>? Warnings = null,

    // Provenance of the snapshot(s) this run carried. Empty for a flat log upload.
    IReadOnlyList<SyncedSiteSummary>? SyncedSites = null,

    string? FileName = null,
    string? SubmittedBy = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    double? DurationMs = null)
{
    public IReadOnlyList<string> Warnings { get; init; } = Warnings ?? [];
    public IReadOnlyList<SyncedSiteSummary> SyncedSites { get; init; } = SyncedSites ?? [];
}

/// <summary>One site touched by an upload, as the sync report and file index describe it.</summary>
public sealed record SyncedSiteSummary(
    string SiteCode,
    string SiteName,
    string SiteId,
    string Region,
    string Provider,
    string Environment,
    string? Vendor,
    string Technologies,
    int? HealthScore,
    string RequestId,
    int SnapshotVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? CapturedAt);
