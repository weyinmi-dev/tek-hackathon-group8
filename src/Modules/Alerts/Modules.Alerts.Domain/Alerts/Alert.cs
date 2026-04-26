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
}

public sealed record AlertRaisedDomainEvent(Guid AlertId, string Code, string Severity, string Region, int SubscribersAffected) : IDomainEvent;
public sealed record AlertAcknowledgedDomainEvent(Guid AlertId, string Code, string Actor) : IDomainEvent;

public static class AlertErrors
{
    public static readonly Error NotFound = Error.NotFound("Alert.NotFound", "Alert not found.");
    public static readonly Error AlreadyAcknowledged = Error.Problem("Alert.AlreadyAcknowledged", "Alert is already acknowledged or resolved.");
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
}
