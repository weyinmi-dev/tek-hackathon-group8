using Application.Abstractions.Messaging;
using Modules.Alerts.Domain.Alerts;
using SharedKernel;

namespace Modules.Alerts.Application.Alerts.GetAlerts;

internal sealed class GetAlertsQueryHandler(IAlertRepository alerts)
    : IQueryHandler<GetAlertsQuery, IReadOnlyList<AlertDto>>
{
    public async Task<Result<IReadOnlyList<AlertDto>>> Handle(GetAlertsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Alert> rows = request switch
        {
            { ActiveOnly: true } => await alerts.ListActiveAsync(cancellationToken),
            { Severity: { } sev } when Enum.TryParse<AlertSeverity>(sev, true, out var parsed)
                => await alerts.ListBySeverityAsync(parsed, cancellationToken),
            _ => await alerts.ListAsync(cancellationToken)
        };

        IReadOnlyList<AlertDto> dtos = rows
            .OrderByDescending(a => a.RaisedAtUtc)
            .Select(a => new AlertDto(
                a.Code, a.Severity.ToWire(), a.Status.ToWire(), a.Title, a.Region, a.TowerCode,
                a.AiCause, a.SubscribersAffected, a.Confidence, FormatRelative(a.RaisedAtUtc)))
            .ToList();

        return Result.Success(dtos);
    }

    private static string FormatRelative(DateTime raisedUtc)
    {
        TimeSpan delta = DateTime.UtcNow - raisedUtc;
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        return $"{(int)delta.TotalDays}d ago";
    }
}
