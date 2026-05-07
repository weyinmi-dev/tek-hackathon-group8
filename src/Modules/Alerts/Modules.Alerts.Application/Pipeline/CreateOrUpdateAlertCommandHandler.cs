using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using SharedKernel;

namespace Modules.Alerts.Application.Pipeline;

internal sealed class CreateOrUpdateAlertCommandHandler(
    IAlertRepository alerts,
    IUnitOfWork unitOfWork,
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

        // 3. No live alert with this fingerprint — create a new one.
        string code = BuildCode(request.AnomalyFingerprint);
        Alert alert = Alert.RaiseFromAnomaly(
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

        return Result.Success(new CreateOrUpdateAlertResult(alert.Id, WasCreated: true));
    }

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
