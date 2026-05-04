using Application.Abstractions.Messaging;

namespace Modules.Alerts.Application.Alerts.GetAlerts;

public sealed record GetAlertsQuery(string? Severity = null, bool ActiveOnly = false) : IQuery<IReadOnlyList<AlertDto>>;

public sealed record AlertDto(
    string Id,
    string Sev,
    string Status,
    string Title,
    string Region,
    string Tower,
    string Cause,
    int Users,
    double Confidence,
    string Time,
    string? AssignedTeam,
    string? DispatchTarget);
