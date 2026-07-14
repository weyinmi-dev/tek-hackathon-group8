using SharedKernel;

namespace Modules.Network.Domain.Ingestion;

/// <summary>
/// Aggregate root for one execution of the network-ops ingestion pipeline. Tracks
/// stage progression, per-stage timings, and final counts so the pipeline run is
/// fully traceable after the fact.
/// </summary>
public sealed class IngestionRun : Entity
{
    private readonly List<StageTiming> _stageTimings = [];

    private IngestionRun(
        Guid id,
        string contentHash,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string submittedBy,
        DateTimeOffset startedAt) : base(id)
    {
        ContentHash = contentHash;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        SubmittedBy = submittedBy;
        StartedAt = startedAt;
        Status = IngestionStatus.Pending;
    }

    private IngestionRun() { }

    public string ContentHash { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    public string SubmittedBy { get; private set; } = null!;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IngestionStatus Status { get; private set; }
    public string? FailureReason { get; private set; }

    public int EventsParsed { get; private set; }
    public int AnomaliesDetected { get; private set; }
    public int AlertsCreated { get; private set; }
    public int AlertsUpdated { get; private set; }
    public int OptimizationsCreated { get; private set; }
    public bool TopologyChanged { get; private set; }

    // ── Synchronisation report ────────────────────────────────────────────────
    // Persisted on the run so the sync-history view can render "what did this upload change?"
    // straight from the run, without recomputing it from six modules' tables after the fact.
    public int RecordsCreated { get; private set; }
    public int RecordsUpdated { get; private set; }
    public int RecordsArchived { get; private set; }
    public int TelemetryRowsAppended { get; private set; }

    /// <summary>
    /// Non-fatal problems, newline-separated. A feed is allowed to be imperfect; the run still
    /// succeeds, but a partially-applied sync must never be presented as a clean one.
    /// </summary>
    public string? Warnings { get; private set; }

    public IReadOnlyList<StageTiming> StageTimings => _stageTimings.AsReadOnly();

    /// <summary>Wall-clock duration of the run — what the history view shows as "processing time".</summary>
    public TimeSpan? Duration => CompletedAt is { } done ? done - StartedAt : null;

    public static IngestionRun Start(
        string contentHash,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string submittedBy,
        DateTimeOffset startedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        ArgumentOutOfRangeException.ThrowIfNegative(fileSizeBytes);

        return new IngestionRun(
            Guid.NewGuid(),
            contentHash,
            fileName,
            contentType,
            fileSizeBytes,
            submittedBy,
            startedAt);
    }

    public void TransitionTo(IngestionStatus next)
    {
        if (!IngestionStatusTransitions.CanTransition(Status, next))
        {
            throw new InvalidOperationException(
                $"Illegal ingestion-run transition: {Status} → {next} (run {Id}).");
        }

        Status = next;
    }

    public void RecordStageTiming(StageTiming timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        _stageTimings.Add(timing);
    }

    public void RecordParsedCount(int eventsParsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventsParsed);
        EventsParsed = eventsParsed;
    }

    public void Complete(IngestionRunCounts counts, DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (Status != IngestionStatus.Projecting)
        {
            throw new InvalidOperationException(
                $"Cannot complete a run in status {Status} (run {Id}).");
        }

        AnomaliesDetected = counts.AnomaliesDetected;
        AlertsCreated = counts.AlertsCreated;
        AlertsUpdated = counts.AlertsUpdated;
        OptimizationsCreated = counts.OptimizationsCreated;
        TopologyChanged = counts.TopologyChanged;
        RecordsCreated = counts.RecordsCreated;
        RecordsUpdated = counts.RecordsUpdated;
        RecordsArchived = counts.RecordsArchived;
        TelemetryRowsAppended = counts.TelemetryRowsAppended;
        Warnings = counts.Warnings.Count > 0 ? string.Join('\n', counts.Warnings) : null;
        CompletedAt = completedAt;
        Status = IngestionStatus.Completed;
    }

    public void Fail(string reason, DateTimeOffset failedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!IngestionStatusTransitions.CanTransition(Status, IngestionStatus.Failed))
        {
            throw new InvalidOperationException(
                $"Cannot fail a run already in terminal status {Status} (run {Id}).");
        }

        FailureReason = reason;
        CompletedAt = failedAt;
        Status = IngestionStatus.Failed;
    }
}

public sealed record IngestionRunCounts(
    int AnomaliesDetected,
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    bool TopologyChanged,
    int RecordsCreated = 0,
    int RecordsUpdated = 0,
    int RecordsArchived = 0,
    int TelemetryRowsAppended = 0,
    IReadOnlyList<string>? Warnings = null)
{
    public IReadOnlyList<string> Warnings { get; init; } = Warnings ?? [];
}
