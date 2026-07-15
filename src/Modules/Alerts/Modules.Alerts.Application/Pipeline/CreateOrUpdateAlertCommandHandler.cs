using Application.Abstractions.Events;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using SharedKernel;

namespace Modules.Alerts.Application.Pipeline;

internal sealed class CreateOrUpdateAlertCommandHandler(
    IAlertRepository alerts,
    IUnitOfWork unitOfWork,
    IEventBus eventBus,
    ILogger<CreateOrUpdateAlertCommandHandler> logger)
    : ICommandHandler<CreateOrUpdateAlertCommand, CreateOrUpdateAlertResult>
{
    public async Task<Result<CreateOrUpdateAlertResult>> Handle(
        CreateOrUpdateAlertCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Hint from the decision engine: if it gave us an ExistingAlertId, try that first.
        Alert? existing = null;
        if (request.ExistingAlertId is { } existingId)
        {
            existing = await alerts.GetByIdAsync(existingId, cancellationToken);
        }

        // 2. Defensive double-check by fingerprint. Guards against the (rare) race where
        //    two ingestion runs targeting the same anomaly land between snapshot read
        //    and persistence.
        existing ??= await alerts.GetActiveByFingerprintAsync(request.AnomalyFingerprint, cancellationToken);

        if (existing is not null)
        {
            Result recurrence = existing.RegisterRecurrence(
                request.Severity,
                request.Confidence,
                request.AiCause,
                request.DetectedAtUtc);

            if (recurrence.IsFailure)
            {
                return Result.Failure<CreateOrUpdateAlertResult>(recurrence.Error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Alert {AlertId} ({Code}) recurrence #{Occurrences} for fingerprint {Fingerprint}",
                existing.Id, existing.Code, existing.OccurrenceCount, request.AnomalyFingerprint);

            return Result.Success(new CreateOrUpdateAlertResult(existing.Id, WasCreated: false));
        }

        // 3. No LIVE alert — but there may be a RESOLVED one with this fingerprint. The alert's Code is
        //    derived from the fingerprint and is uniquely indexed, so inserting a "new" alert here
        //    would collide with the resolved one's code and fail the whole ingestion run. This is
        //    exactly what happens when an OSS alarm clears and is later reported again: the alarm id
        //    is stable, so the fingerprint is too.
        //
        //    Reopen it. That is both the only thing the schema permits and the truthful model — the
        //    same alarm coming back is a recurrence, not a different incident.
        Alert? resolved = await alerts.GetByFingerprintAsync(request.AnomalyFingerprint, cancellationToken);
        if (resolved is not null)
        {
            resolved.Reopen(request.Severity, request.Confidence, request.AiCause, request.DetectedAtUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Alert {AlertId} ({Code}) reopened — fingerprint {Fingerprint} was reported again after being resolved",
                resolved.Id, resolved.Code, request.AnomalyFingerprint);

            // Announced as a new alarm: the condition is live again and deserves an investigation,
            // which a silent recurrence bump would not trigger.
            await PublishAlarmReceivedAsync(resolved.Id, resolved.Code, request, cancellationToken);

            return Result.Success(new CreateOrUpdateAlertResult(resolved.Id, WasCreated: true));
        }

        // 4. Genuinely new — create it.
        string code = BuildCode(request.AnomalyFingerprint);
        var alert = Alert.RaiseFromAnomaly(
            code: code,
            severity: request.Severity,
            title: request.Title,
            region: request.Region,
            towerCode: request.TowerCode,
            aiCause: request.AiCause,
            subscribersAffected: request.SubscribersAffected,
            confidence: request.Confidence,
            anomalyFingerprint: request.AnomalyFingerprint,
            detectedAtUtc: request.DetectedAtUtc);

        await alerts.AddAsync(alert, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Alert {AlertId} ({Code}) raised from anomaly fingerprint {Fingerprint}",
            alert.Id, alert.Code, request.AnomalyFingerprint);

        // Announce the new alarm. Published on the create and reopen paths only — an ordinary
        // recurrence is the same alarm still firing and must not re-open an investigation.
        // Subscribers (currently the AI module's IncidentInvestigationWorkflow) react on their own;
        // Alerts does not know they exist, and nothing downstream of this line can change the alert
        // or optimization counts.
        await PublishAlarmReceivedAsync(alert.Id, alert.Code, request, cancellationToken);

        return Result.Success(new CreateOrUpdateAlertResult(alert.Id, WasCreated: true));
    }

    private Task PublishAlarmReceivedAsync(
        Guid alertId, string code, CreateOrUpdateAlertCommand request, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new AlarmReceived(
                Id: Guid.NewGuid(),
                AlertId: alertId,
                Code: code,
                Severity: request.Severity.ToString(),
                TowerCode: request.TowerCode,
                Region: request.Region,
                Title: request.Title,
                AiCause: request.AiCause,
                Confidence: request.Confidence,
                DetectedAtUtc: request.DetectedAtUtc),
            cancellationToken);

    /// <summary>
    /// Stable, prefix-truncated code derived from the anomaly fingerprint. Keeps the
    /// existing 32-char Code column happy and gives operators a deterministic identifier
    /// they can find again if they ingest the same data twice.
    /// </summary>
    private static string BuildCode(string anomalyFingerprint)
    {
        // Code column is HasMaxLength(32). Prefix with "AL-" + first 24 fingerprint chars.
        string prefix = anomalyFingerprint.Length >= 24
            ? anomalyFingerprint[..24]
            : anomalyFingerprint;
        return $"AL-{prefix}";
    }
}
