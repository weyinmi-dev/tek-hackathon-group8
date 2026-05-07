using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Alerts.Application.Pipeline;
using Modules.Alerts.Domain.Alerts;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using SharedKernel;

namespace Modules.Alerts.Infrastructure.Pipeline;

/// <summary>
/// Cross-module adapter: implements Network.Application's <see cref="IAlertActionExecutor"/>
/// port by dispatching one <see cref="CreateOrUpdateAlertCommand"/> per request through
/// MediatR. Each command rides the standard logging / validation pipeline behaviors.
/// Failures are aggregated — the first failure short-circuits the batch with that error,
/// so partial-success state is avoided (Stage 4 is wrapped in a single ingestion-run
/// attempt; partial mutations would leave the run in a confusing state).
/// </summary>
internal sealed class AlertActionExecutor(
    ISender sender,
    ILogger<AlertActionExecutor> logger) : IAlertActionExecutor
{
    public async Task<Result<AlertActionsResult>> ExecuteAsync(
        IReadOnlyList<AlertActionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        int created = 0;
        int updated = 0;

        foreach (AlertActionRequest request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var command = new CreateOrUpdateAlertCommand(
                AnomalyFingerprint: request.AnomalyFingerprint,
                ExistingAlertId: request.ExistingAlertId,
                Severity: ParseSeverity(request.SeverityWire),
                TowerCode: request.TowerCode,
                Region: request.Region,
                Title: request.Title,
                AiCause: request.AiCause,
                Confidence: request.Confidence,
                SubscribersAffected: request.SubscribersAffected,
                DetectedAtUtc: request.DetectedAtUtc);

            Result<CreateOrUpdateAlertResult> result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                logger.LogError(
                    "Alert action failed for fingerprint {Fingerprint}: {ErrorCode} {ErrorDescription}",
                    request.AnomalyFingerprint, result.Error.Code, result.Error.Description);
                return Result.Failure<AlertActionsResult>(result.Error);
            }

            if (result.Value.WasCreated) created++;
            else updated++;
        }

        return Result.Success(new AlertActionsResult(created, updated));
    }

    private static AlertSeverity ParseSeverity(string wire) => wire?.ToUpperInvariant() switch
    {
        "CRITICAL" => AlertSeverity.Critical,
        "WARN" => AlertSeverity.Warn,
        _ => AlertSeverity.Info
    };
}
