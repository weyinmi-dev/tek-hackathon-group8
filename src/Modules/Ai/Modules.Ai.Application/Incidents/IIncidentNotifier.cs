namespace Modules.Ai.Application.Incidents;

/// <summary>One step an engineer should take, in the order the runbook says to take it.</summary>
public sealed record RunbookAction(int Order, string Action, string Rationale);

/// <summary>
/// The finished investigation: what the alarm was, what the agent concluded, and what to do about it.
/// </summary>
public sealed record IncidentNotification(
    Guid AlertId,
    string AlertCode,
    string Severity,
    string TowerCode,
    string Region,
    string RootCause,
    IReadOnlyList<RunbookAction> Actions,
    IReadOnlyList<string> CorrelatedAlertCodes);

/// <summary>
/// The terminal step of IncidentInvestigationWorkflow. A port, not a concrete channel, because where
/// a recommendation should land (log, dashboard push, email, pager) is a deployment decision and the
/// workflow must not encode one. Today the only implementation writes a structured log record.
/// </summary>
public interface IIncidentNotifier
{
    Task NotifyAsync(IncidentNotification notification, CancellationToken cancellationToken = default);
}
