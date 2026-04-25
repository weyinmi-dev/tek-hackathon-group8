namespace Modules.Alerts.Api;

public sealed record AlertSnapshot(
    string Code,
    string Severity,
    string Status,
    string Title,
    string Region,
    string TowerCode,
    string Cause,
    int SubscribersAffected,
    double Confidence,
    DateTime RaisedAtUtc);

public interface IAlertsApi
{
    Task<IReadOnlyList<AlertSnapshot>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertSnapshot>> ListAllAsync(CancellationToken cancellationToken = default);
}
