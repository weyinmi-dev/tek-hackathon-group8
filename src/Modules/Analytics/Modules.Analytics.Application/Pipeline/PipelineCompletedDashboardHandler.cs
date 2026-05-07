using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Analytics.Domain;
using Modules.Analytics.Domain.Ingestion;
using Modules.Network.Application.Ingestion.Pipeline;

namespace Modules.Analytics.Application.Pipeline;

/// <summary>
/// Stage 5 projection: persists a dashboard read-model row for every completed ingestion
/// run. Idempotent — re-firing the notification for the same run is a no-op (guarded by
/// <c>ExistsForRunAsync</c>) so MediatR's at-least-once semantics don't produce duplicates.
/// Failures here are swallowed by the orchestrator's projection try/catch; the run still
/// completes. That's by design — a dashboard miss is recoverable, an aborted run isn't.
/// </summary>
internal sealed class PipelineCompletedDashboardHandler(
    IIngestionDashboardRepository dashboard,
    IUnitOfWork unitOfWork,
    ILogger<PipelineCompletedDashboardHandler> logger) : INotificationHandler<PipelineCompletedNotification>
{
    public async Task Handle(PipelineCompletedNotification notification, CancellationToken cancellationToken)
    {
        if (await dashboard.ExistsForRunAsync(notification.IngestionRunId, cancellationToken))
        {
            logger.LogDebug(
                "Dashboard entry for run {IngestionRunId} already exists — skipping",
                notification.IngestionRunId);
            return;
        }

        IngestionDashboardEntry entry = IngestionDashboardEntry.Create(
            ingestionRunId: notification.IngestionRunId,
            contentHash: notification.ContentHash,
            fileName: notification.FileName,
            completedAt: notification.CompletedAt,
            eventsParsed: notification.EventsParsed,
            anomaliesDetected: notification.AnomaliesDetected,
            alertsCreated: notification.AlertsCreated,
            alertsUpdated: notification.AlertsUpdated,
            optimizationsCreated: notification.OptimizationsCreated,
            topologyChanged: notification.TopologyChanged);

        await dashboard.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Dashboard entry created for ingestion run {IngestionRunId} ({FileName})",
            notification.IngestionRunId, notification.FileName);
    }
}
