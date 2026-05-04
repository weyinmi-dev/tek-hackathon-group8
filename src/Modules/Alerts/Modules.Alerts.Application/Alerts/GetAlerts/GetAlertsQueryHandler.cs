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
            { Severity: { } sev } when Enum.TryParse<AlertSeverity>(sev, true, out AlertSeverity parsed)
                => await alerts.ListBySeverityAsync(parsed, cancellationToken),
            _ => await alerts.ListAsync(cancellationToken)
        };

        IReadOnlyList<AlertDto> dtos = rows
            .OrderByDescending(a => a.RaisedAtUtc)
            .Select(a => new AlertDto(
                a.Code, a.Severity.ToWire(), a.Status.ToWire(), a.Title, a.Region, a.TowerCode,
                a.AiCause, a.SubscribersAffected, a.Confidence, FormatRelative(a.RaisedAtUtc),
                a.AssignedTeam, a.DispatchTarget))
            .ToList();

        return Result.Success(dtos);
    }

    private static string FormatRelative(DateTime raisedUtc)
    {
        TimeSpan delta = DateTime.UtcNow - raisedUtc;
#pragma warning disable IDE0011 // Add braces
        if (delta.TotalMinutes < 1) return "just now";
#pragma warning restore IDE0011 // Add braces
#pragma warning disable IDE0011 // Add braces
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
#pragma warning restore IDE0011 // Add braces
#pragma warning disable IDE0011 // Add braces
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
#pragma warning restore IDE0011 // Add braces
        return $"{(int)delta.TotalDays}d ago";
    }
}
