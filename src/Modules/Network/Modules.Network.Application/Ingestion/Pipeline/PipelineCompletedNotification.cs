using MediatR;

namespace Modules.Network.Application.Ingestion.Pipeline;

/// <summary>
/// Published by the orchestrator after Stage 4 succeeds and before the run is marked
/// Completed. Subscribed by the Analytics module's dashboard projection handler (and
/// any future projections — copilot KB indexing, SignalR push, etc.). Handlers run
/// synchronously via MediatR; if any handler fails, the orchestrator still completes
/// the run — projections are best-effort by design and shouldn't block ingestion.
/// </summary>
public sealed record PipelineCompletedNotification(
    Guid IngestionRunId,
    string ContentHash,
    string FileName,
    int EventsParsed,
    int AnomaliesDetected,
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    bool TopologyChanged,
    DateTimeOffset CompletedAt) : INotification;
