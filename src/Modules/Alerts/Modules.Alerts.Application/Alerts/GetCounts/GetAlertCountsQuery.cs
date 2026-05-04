using Application.Abstractions.Messaging;

namespace Modules.Alerts.Application.Alerts.GetCounts;

public sealed record GetAlertCountsQuery : IQuery<AlertCountsDto>;

public sealed record AlertCountsDto(int All, int Critical, int Warn, int Info);
