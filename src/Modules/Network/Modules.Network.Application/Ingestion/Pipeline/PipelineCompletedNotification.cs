using Application.Abstractions.Events;

namespace Modules.Network.Application.Ingestion.Pipeline;

/// <summary>
/// Stage-5 integration event. The orchestrator hands it to <c>IEventBus</c> which writes
/// it onto the <c>InMemoryMessageQueue</c>; the existing <c>IntegrationEventProcessorJob</c>
/// hosted service drains the queue and re-publishes via MediatR. This decouples the
/// publish step from the orchestrator's request lifetime — slow or failing subscribers
/// (the dashboard projection, future copilot KB indexer, etc.) don't fail the run.
///
/// NOT crash-safe: the queue is in-memory. Surviving a restart between Stage 4 commit
/// and event publish requires a durable outbox table — intentionally deferred (see
/// the deferred follow-ups in PR 6's summary).
/// </summary>
public sealed record PipelineCompletedNotification(
    Guid Id,
    Guid IngestionRunId,
    string ContentHash,
    string FileName,
    int EventsParsed,
    int AnomaliesDetected,
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    bool TopologyChanged,
    DateTimeOffset CompletedAt,

    // ── Synchronisation outcome ───────────────────────────────────────────────
    // Carried on the event so subscribers can act on what the upload actually did without going
    // back to the database for it. The notification feed reads these to decide what is worth
    // telling an operator about.
    int RecordsCreated = 0,
    int RecordsUpdated = 0,
    int RecordsArchived = 0,
    int CriticalAlertsRaised = 0,
    int WarningCount = 0,
    string? SubmittedBy = null,
    IReadOnlyList<string>? SiteCodes = null) : IntegrationEvent(Id)
{
    public IReadOnlyList<string> SiteCodes { get; init; } = SiteCodes ?? [];
}

/// <summary>
/// Raised when a run fails. The completed event deliberately never fires for a failed run, so
/// without this a synchronisation failure would be invisible to anything downstream — including the
/// operator, who is exactly the person who needs to know that the feed stopped landing.
/// </summary>
public sealed record PipelineFailedNotification(
    Guid Id,
    Guid IngestionRunId,
    string FileName,
    string Reason,
    string SubmittedBy,
    DateTimeOffset FailedAt) : IntegrationEvent(Id);
