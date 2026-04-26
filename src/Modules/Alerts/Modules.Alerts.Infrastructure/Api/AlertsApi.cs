using Modules.Alerts.Api;
using Modules.Alerts.Domain.Alerts;

namespace Modules.Alerts.Infrastructure.Api;

internal sealed class AlertsApi(IAlertRepository alerts) : IAlertsApi
{
    public async Task<IReadOnlyList<AlertSnapshot>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Alert> rows = await alerts.ListActiveAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<AlertSnapshot>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Alert> rows = await alerts.ListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    private static AlertSnapshot Map(Alert a) =>
        new(a.Code, a.Severity.ToWire(), a.Status.ToWire(), a.Title, a.Region, a.TowerCode,
            a.AiCause, a.SubscribersAffected, a.Confidence, a.RaisedAtUtc);
}
