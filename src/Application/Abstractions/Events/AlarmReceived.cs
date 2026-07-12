namespace Application.Abstractions.Events;

/// <summary>
/// Raised when a *new* alert is created — not on recurrence, which is the same alarm firing again.
/// This is the entry point of the directive's headline flow: AlarmReceived → Investigation →
/// Recommendation → Notification (Phase 2 §7.3).
///
/// It lives in the shared kernel rather than in Alerts.Application for the same reason
/// <see cref="DocumentUploaded"/> does: the AI module subscribes to it, and the dependency rules
/// forbid Ai.* from referencing another module's Application layer. Alerts announces a fact; whoever
/// cares reacts. Alerts does not know the investigation exists.
/// </summary>
/// <param name="Id">Event identity (idempotency / tracing).</param>
/// <param name="AlertId">The alert that was raised.</param>
/// <param name="Code">Human-facing alert code (AL-…), stable across a fingerprint.</param>
/// <param name="Severity">Severity as its string name — the shared kernel does not reference the Alerts domain enum.</param>
/// <param name="TowerCode">Tower the anomaly was detected on.</param>
/// <param name="Region">Region the tower sits in.</param>
/// <param name="Title">Short description of the anomaly.</param>
/// <param name="AiCause">The cause recorded at detection time — the investigation's starting hypothesis, not its conclusion.</param>
/// <param name="Confidence">Detection confidence, 0–1.</param>
/// <param name="DetectedAtUtc">When the anomaly occurred (not when the row was written).</param>
public sealed record AlarmReceived(
    Guid Id,
    Guid AlertId,
    string Code,
    string Severity,
    string TowerCode,
    string Region,
    string Title,
    string AiCause,
    double Confidence,
    DateTime DetectedAtUtc) : IIntegrationEvent;
