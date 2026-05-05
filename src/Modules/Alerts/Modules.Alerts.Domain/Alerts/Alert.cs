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

    public static Alert Raise(
        string code, AlertSeverity severity, string title, string region, string towerCode,
        string aiCause, int subscribersAffected, double confidence, AlertStatus status, DateTime raisedAtUtc)
    {
        var a = new Alert(Guid.NewGuid(), code, severity, title, region, towerCode, aiCause, subscribersAffected, confidence, status, raisedAtUtc);
        a.Raise(new AlertRaisedDomainEvent(a.Id, code, severity.ToWire(), region, subscribersAffected));
        return a;
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

public static class AlertErrors
{
    public static readonly Error NotFound = Error.NotFound("Alert.NotFound", "Alert not found.");
    public static readonly Error AlreadyAcknowledged = Error.Problem("Alert.AlreadyAcknowledged", "Alert is already acknowledged or resolved.");
    public static readonly Error AlreadyResolved = Error.Problem("Alert.AlreadyResolved", "Alert is already resolved.");
    public static readonly Error InvalidAssignment = Error.Problem("Alert.InvalidAssignment", "Team name is required.");
    public static readonly Error InvalidDispatch = Error.Problem("Alert.InvalidDispatch", "Dispatch target is required.");
}

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> ListBySeverityAsync(AlertSeverity severity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<Alert?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Alert> alerts, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<AlertSeverity, int>> CountBySeverityAsync(CancellationToken cancellationToken = default);
}
