using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Incidents;

namespace Modules.Ai.Infrastructure.Incidents;

/// <summary>
/// Records a finished investigation as a structured log entry. This is the notification channel the
/// system has today: there is no incident table and no operator inbox to push to, and inventing one
/// here would be building a feature nobody asked for. The port exists so that adding a real channel
/// (dashboard push, pager, email) is a registration change, not a workflow change.
/// </summary>
internal sealed class LoggingIncidentNotifier(ILogger<LoggingIncidentNotifier> logger) : IIncidentNotifier
{
    public Task NotifyAsync(IncidentNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Incident investigation complete for alert {AlertCode} ({Severity}) on {TowerCode}/{Region}. " +
            "Root cause: {RootCause}. Correlated alerts: {CorrelatedAlerts}. Recommended actions: {Actions}",
            notification.AlertCode,
            notification.Severity,
            notification.TowerCode,
            notification.Region,
            notification.RootCause,
            notification.CorrelatedAlertCodes.Count == 0
                ? "none"
                : string.Join(", ", notification.CorrelatedAlertCodes),
            string.Join(" | ", notification.Actions.Select(a => $"{a.Order}. {a.Action} ({a.Rationale})")));

        return Task.CompletedTask;
    }
}
