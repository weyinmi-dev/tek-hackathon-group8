using Application.Abstractions.Events;
using MediatR;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Modules.Ai.Agents.Workflows;
using Modules.Ai.Agents.Workflows.IncidentInvestigation;
using Modules.Ai.Application.Workflows;

namespace Modules.Ai.Infrastructure.Hosting;

/// <summary>
/// Runs <c>IncidentInvestigationWorkflow</c> in response to <see cref="AlarmReceived"/> (Phase 2 §7.3,
/// M12b). Like the ingestion host, this is the durability seam: the workflow graph never mentions
/// checkpoints; the host binds the checkpoint manager to the Postgres store, so a crash after the
/// model call resumes from it rather than paying for the call again.
/// </summary>
/// <remarks>
/// The investigation is strictly <b>additive</b>. It reads alerts and writes a notification; it never
/// creates or mutates an alert, an optimization, or an ingestion run. That is the M12b contract — the
/// pipeline's structured counts must be identical whether this host is registered or not — and it is
/// why a failure in here is caught and logged rather than propagated: an investigation that falls over
/// must not fail the ingestion whose alert triggered it.
/// </remarks>
internal sealed class IncidentInvestigationWorkflowHost(
    IncidentInvestigationWorkflowBuilder workflowBuilder,
    IWorkflowCheckpointStore checkpointStore,
    ILogger<IncidentInvestigationWorkflowHost> logger) : INotificationHandler<AlarmReceived>
{
    public async Task Handle(AlarmReceived notification, CancellationToken cancellationToken)
    {
        // Deterministic run id keyed to the alert: a restart after a crash finds the same run and
        // resumes it, and the same alarm cannot be investigated twice concurrently.
        string runId = $"incident-{notification.AlertId:N}";

        try
        {
            var manager = CheckpointManager.CreateJson(new PostgresCheckpointStore(checkpointStore));
            Workflow workflow = workflowBuilder.Build();

            IReadOnlyList<WorkflowCheckpointRef> existing =
                await checkpointStore.ListAsync(runId, null, cancellationToken);

            StreamingRun run;
            if (existing.Count > 0)
            {
                var latest = new CheckpointInfo(runId, existing[^1].CheckpointId);
                logger.LogInformation(
                    "Resuming IncidentInvestigationWorkflow for alert {AlertCode} from checkpoint {CheckpointId}.",
                    notification.Code, latest.CheckpointId);
                run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, cancellationToken);
            }
            else
            {
                var request = new InvestigateAlarmRequest(
                    notification.AlertId,
                    notification.Code,
                    notification.Severity,
                    notification.TowerCode,
                    notification.Region,
                    notification.Title,
                    notification.AiCause,
                    notification.Confidence,
                    notification.DetectedAtUtc);

                run = await InProcessExecution.RunStreamingAsync(
                    workflow, request, manager, sessionId: runId, cancellationToken: cancellationToken);
            }

            await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
            {
                if (evt is WorkflowOutputEvent { Data: InvestigationCompleted outcome })
                {
                    logger.LogInformation(
                        "IncidentInvestigationWorkflow finished for alert {AlertCode}: {ActionCount} recommended " +
                        "actions, {CorrelatedAlerts} correlated alerts.",
                        notification.Code, outcome.ActionCount, outcome.CorrelatedAlerts);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Additive means additive: the alert is already committed and the ingestion run is not
            // waiting on us. Swallow, record, move on.
            logger.LogError(ex,
                "IncidentInvestigationWorkflow failed for alert {AlertCode}. The alert stands; only the " +
                "investigation was lost.", notification.Code);
        }
    }
}
