using SharedKernel;

namespace Modules.Alerts.Domain.Alerts;

public sealed class Alert : Entity
{
    private Alert(
        Guid id, string code, AlertSeverity severity, string title, string region,
        string towerCode, string aiCause, int subscribersAffected, double confidence,
        AlertStatus status, DateTime raisedAtUtc) : base(id)
    {
        Code = code;
        Severity = severity;
        Title = title;
        Region = region;
        TowerCode = towerCode;
        AiCause = aiCause;
        SubscribersAffected = subscribersAffected;
        Confidence = confidence;
        Status = status;
        RaisedAtUtc = raisedAtUtc;
    }

    private Alert() { }

    public string Code { get; private set; } = null!;
    public AlertSeverity Severity { get; private set; }
    public string Title { get; private set; } = null!;
    public string Region { get; private set; } = null!;
    public string TowerCode { get; private set; } = null!;
    public string AiCause { get; private set; } = null!;
    public int SubscribersAffected { get; private set; }
    public double Confidence { get; private set; }
    public AlertStatus Status { get; private set; }
    public DateTime RaisedAtUtc { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public string? AcknowledgedBy { get; private set; }

    // Operator follow-up state. Assignment is a manager-tier action; dispatch is engineer-tier.
    // Both are free-text by design — teams + dispatch targets vary by region and we don't want
    // an enum that needs a migration every time NOC re-orgs.
    public string? AssignedTeam { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public string? AssignedBy { get; private set; }
    public string? DispatchTarget { get; private set; }
    public DateTime? DispatchedAtUtc { get; private set; }
    public string? DispatchedBy { get; private set; }

    // ── Stage-4 dedup fields ──────────────────────────────────────────────────
    // Populated only by alerts created from the AI ingestion pipeline. Older alerts
    // (raised manually or pre-PR-5) keep these as NULL and don't participate in
    // fingerprint-based deduplication, so existing behaviour is preserved.
    public string? AnomalyFingerprint { get; private set; }
    public int? OccurrenceCount { get; private set; }
    public DateTime? LastSeenAtUtc { get; private set; }

    public static Alert Raise(
        string code, AlertSeverity severity, string title, string region, string towerCode,
        string aiCause, int subscribersAffected, double confidence, AlertStatus status, DateTime raisedAtUtc)
    {
        var a = new Alert(Guid.NewGuid(), code, severity, title, region, towerCode, aiCause, subscribersAffected, confidence, status, raisedAtUtc);
        a.Raise(new AlertRaisedDomainEvent(a.Id, code, severity.ToWire(), region, subscribersAffected));
        return a;
    }

    /// <summary>
    /// Pipeline-created alert with a deterministic fingerprint. The orchestrator chooses
    /// CREATE vs UPDATE upstream (in the decision engine); this factory is only called
    /// when no live alert with the same fingerprint exists.
    /// </summary>
    public static Alert RaiseFromAnomaly(
        string code, AlertSeverity severity, string title, string region, string towerCode,
        string aiCause, int subscribersAffected, double confidence,
        string anomalyFingerprint, DateTime detectedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anomalyFingerprint);

        var a = new Alert(
            Guid.NewGuid(), code, severity, title, region, towerCode,
            aiCause, subscribersAffected, confidence, AlertStatus.Active, detectedAtUtc);
        a.AnomalyFingerprint = anomalyFingerprint;
        a.OccurrenceCount = 1;
        a.LastSeenAtUtc = detectedAtUtc;
        a.Raise(new AlertRaisedDomainEvent(a.Id, code, severity.ToWire(), region, subscribersAffected));
        return a;
    }

    /// <summary>
    /// Records a recurrence of the same anomaly: bumps the occurrence counter, refreshes
    /// the last-seen timestamp, and escalates severity if the new evidence is stronger.
    /// The alert's lifecycle status (Acknowledged, Investigating, etc.) is intentionally
    /// not touched — operators have already engaged with the alert and shouldn't lose context.
    /// </summary>
    public Result RegisterRecurrence(
        AlertSeverity newSeverity,
        double newConfidence,
        string? updatedAiCause,
        DateTime detectedAtUtc)
    {
        if (AnomalyFingerprint is null)
        {
            return Result.Failure(AlertErrors.NotFingerprintTracked);
        }

        if (Status == AlertStatus.Resolved)
        {
            // Re-emerging anomalies on resolved alerts must produce a NEW alert; the
            // decision engine already enforces this, but defend the invariant here too.
            return Result.Failure(AlertErrors.AlreadyResolved);
        }

        OccurrenceCount = (OccurrenceCount ?? 0) + 1;
        LastSeenAtUtc = detectedAtUtc;

        if (newSeverity > Severity)
        {
            Severity = newSeverity;
        }

        // Confidence reflects the strongest signal seen so far.
        if (newConfidence > Confidence)
        {
            Confidence = newConfidence;
        }

        if (!string.IsNullOrWhiteSpace(updatedAiCause))
        {
            AiCause = updatedAiCause;
        }

        Raise(new AlertRecurredDomainEvent(Id, Code, OccurrenceCount.Value, Severity.ToWire()));
        return Result.Success();
    }

    /// <summary>
    /// Closes the alert because the condition that raised it is gone — the upstream alarm cleared,
    /// or an OSS snapshot stopped reporting it.
    ///
    /// <see cref="AlertStatus.Resolved"/> has existed since the beginning but nothing could reach
    /// it: alerts could be acknowledged, assigned and dispatched, never closed. Synchronisation
    /// needs it, because an alarm that clears upstream must stop showing as live here.
    ///
    /// Idempotent — resolving an already-resolved alert is a no-op, so a repeated snapshot that
    /// keeps omitting the alarm does not keep rewriting the row.
    /// </summary>
    public bool Resolve(string reason, DateTime resolvedAtUtc)
    {
        if (Status == AlertStatus.Resolved)
        {
            return false;
        }

        Status = AlertStatus.Resolved;
        LastSeenAtUtc = resolvedAtUtc;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            AiCause = reason;
        }

        return true;
    }

    public Result Acknowledge(string actor)
    {
        if (Status is AlertStatus.Acknowledged or AlertStatus.Resolved)
        {
            return Result.Failure(AlertErrors.AlreadyAcknowledged);
        }


        Status = AlertStatus.Acknowledged;
        AcknowledgedAtUtc = DateTime.UtcNow;
        AcknowledgedBy = actor;
        Raise(new AlertAcknowledgedDomainEvent(Id, Code, actor));
        return Result.Success();
    }

    /// <summary>
    /// Manager-tier action: assign the incident to a NOC team. Idempotent — re-assigning
    /// to the same team is a no-op so retries don't spam audit. Status moves into
    /// Investigating if it's currently Active.
    /// </summary>
    public Result AssignToTeam(string team, string actor)
    {
        if (string.IsNullOrWhiteSpace(team))
        {
            return Result.Failure(AlertErrors.InvalidAssignment);
        }
        if (Status is AlertStatus.Resolved)
        {
            return Result.Failure(AlertErrors.AlreadyResolved);
        }

        AssignedTeam = team.Trim();
        AssignedAtUtc = DateTime.UtcNow;
        AssignedBy = actor;
        if (Status == AlertStatus.Active)
        {
            Status = AlertStatus.Investigating;
        }
        Raise(new AlertAssignedDomainEvent(Id, Code, AssignedTeam, actor));
        return Result.Success();
    }

    /// <summary>
    /// Engineer-tier action: log a field dispatch (truck, technician, vendor, etc.). Status
    /// moves into Investigating if it was Active. Always logs a fresh dispatch event so the
    /// audit trail captures every send-out.
    /// </summary>
    public Result DispatchField(string target, string actor)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return Result.Failure(AlertErrors.InvalidDispatch);
        }
        if (Status is AlertStatus.Resolved)
        {
            return Result.Failure(AlertErrors.AlreadyResolved);
        }

        DispatchTarget = target.Trim();
        DispatchedAtUtc = DateTime.UtcNow;
        DispatchedBy = actor;
        if (Status == AlertStatus.Active)
        {
            Status = AlertStatus.Investigating;
        }
        Raise(new AlertDispatchedDomainEvent(Id, Code, DispatchTarget, actor));
        return Result.Success();
    }
}

public sealed record AlertRaisedDomainEvent(Guid AlertId, string Code, string Severity, string Region, int SubscribersAffected) : IDomainEvent;
public sealed record AlertAcknowledgedDomainEvent(Guid AlertId, string Code, string Actor) : IDomainEvent;
public sealed record AlertAssignedDomainEvent(Guid AlertId, string Code, string Team, string Actor) : IDomainEvent;
public sealed record AlertDispatchedDomainEvent(Guid AlertId, string Code, string Target, string Actor) : IDomainEvent;
public sealed record AlertRecurredDomainEvent(Guid AlertId, string Code, int OccurrenceCount, string Severity) : IDomainEvent;

public static class AlertErrors
{
    public static readonly Error NotFound = Error.NotFound("Alert.NotFound", "Alert not found.");
    public static readonly Error AlreadyAcknowledged = Error.Problem("Alert.AlreadyAcknowledged", "Alert is already acknowledged or resolved.");
    public static readonly Error AlreadyResolved = Error.Problem("Alert.AlreadyResolved", "Alert is already resolved.");
    public static readonly Error InvalidAssignment = Error.Problem("Alert.InvalidAssignment", "Team name is required.");
    public static readonly Error InvalidDispatch = Error.Problem("Alert.InvalidDispatch", "Dispatch target is required.");
    public static readonly Error NotFingerprintTracked = Error.Problem(
        "Alert.NotFingerprintTracked",
        "Alert was not created from the AI ingestion pipeline; recurrences cannot be applied to it.");
}

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> ListBySeverityAsync(AlertSeverity severity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<Alert?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracking lookup for the dedup handler — returns the fingerprint-bearing alert if one
    /// exists in any non-resolved status. Returns null if no live alert matches.
    /// </summary>
    Task<Alert?> GetActiveByFingerprintAsync(string anomalyFingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read port for <c>IAlertSnapshotProvider</c>: every active fingerprinted alert,
    /// projected to a small snapshot the decision engine can consume.
    /// </summary>
    Task<IReadOnlyList<Alert>> ListActiveFingerprintedAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Alert alert, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Alert> alerts, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<AlertSeverity, int>> CountBySeverityAsync(CancellationToken cancellationToken = default);
}
