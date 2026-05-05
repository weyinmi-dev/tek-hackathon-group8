using Application.Abstractions.Messaging;
using Modules.Alerts.Domain.Alerts;

namespace Modules.Alerts.Application.Pipeline;

/// <summary>
/// Internal Stage-4 command. Dispatched by <c>AlertActionExecutor</c> in
/// Alerts.Infrastructure on behalf of the Network ingestion pipeline. Not exposed
/// outside Alerts — Network goes through <c>IAlertActionExecutor</c>, never directly.
/// </summary>
public sealed record CreateOrUpdateAlertCommand(
    string AnomalyFingerprint,
    Guid? ExistingAlertId,
    AlertSeverity Severity,
    string TowerCode,
    string Region,
    string Title,
    string AiCause,
    double Confidence,
    int SubscribersAffected,
    DateTime DetectedAtUtc) : ICommand<CreateOrUpdateAlertResult>;

public sealed record CreateOrUpdateAlertResult(Guid AlertId, bool WasCreated);
