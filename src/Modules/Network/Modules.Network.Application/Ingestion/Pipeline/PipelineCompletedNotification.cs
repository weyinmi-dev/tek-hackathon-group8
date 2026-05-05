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
    DateTimeOffset CompletedAt) : IntegrationEvent(Id);
