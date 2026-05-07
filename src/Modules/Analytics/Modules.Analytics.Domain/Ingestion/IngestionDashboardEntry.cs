using SharedKernel;

namespace Modules.Analytics.Domain.Ingestion;

/// <summary>
/// Read model populated from <c>PipelineCompletedNotification</c>. One row per completed
/// ingestion run; the dashboard queries this to show recent pipeline activity (counts,
/// fingerprint, file name). Append-only — runs are never updated after they land here.
/// </summary>
public sealed class IngestionDashboardEntry : Entity
{
    private IngestionDashboardEntry(
        Guid id,
        Guid ingestionRunId,
        string contentHash,
        string fileName,
        DateTimeOffset completedAt,
        int eventsParsed,
        int anomaliesDetected,
        int alertsCreated,
        int alertsUpdated,
        int optimizationsCreated,
        bool topologyChanged) : base(id)
    {
        IngestionRunId = ingestionRunId;
        ContentHash = contentHash;
        FileName = fileName;
        CompletedAt = completedAt;
        EventsParsed = eventsParsed;
        AnomaliesDetected = anomaliesDetected;
        AlertsCreated = alertsCreated;
        AlertsUpdated = alertsUpdated;
        OptimizationsCreated = optimizationsCreated;
        TopologyChanged = topologyChanged;
    }

    private IngestionDashboardEntry() { }

    public Guid IngestionRunId { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public DateTimeOffset CompletedAt { get; private set; }
    public int EventsParsed { get; private set; }
    public int AnomaliesDetected { get; private set; }
    public int AlertsCreated { get; private set; }
    public int AlertsUpdated { get; private set; }
    public int OptimizationsCreated { get; private set; }
    public bool TopologyChanged { get; private set; }

    public static IngestionDashboardEntry Create(
        Guid ingestionRunId,
        string contentHash,
        string fileName,
        DateTimeOffset completedAt,
        int eventsParsed,
        int anomaliesDetected,
        int alertsCreated,
        int alertsUpdated,
        int optimizationsCreated,
        bool topologyChanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return new IngestionDashboardEntry(
            Guid.NewGuid(),
            ingestionRunId,
            contentHash,
            fileName,
            completedAt,
            eventsParsed,
            anomaliesDetected,
            alertsCreated,
            alertsUpdated,
            optimizationsCreated,
            topologyChanged);
    }
}

public interface IIngestionDashboardRepository
{
    Task AddAsync(IngestionDashboardEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotency check — the projection handler should be safe to re-fire on the same run.
    /// </summary>
    Task<bool> ExistsForRunAsync(Guid ingestionRunId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngestionDashboardEntry>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
}
