using Application.Abstractions.Messaging;
using Modules.Alerts.Domain.Alerts;
using SharedKernel;

namespace Modules.Alerts.Application.Alerts.GetCounts;

internal sealed class GetAlertCountsQueryHandler(IAlertRepository alerts)
    : IQueryHandler<GetAlertCountsQuery, AlertCountsDto>
{
    public async Task<Result<AlertCountsDto>> Handle(GetAlertCountsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<AlertSeverity, int> bySeverity = await alerts.CountBySeverityAsync(cancellationToken);
        int critical = bySeverity.GetValueOrDefault(AlertSeverity.Critical);
        int warn = bySeverity.GetValueOrDefault(AlertSeverity.Warn);
        int info = bySeverity.GetValueOrDefault(AlertSeverity.Info);
        return Result.Success(new AlertCountsDto(critical + warn + info, critical, warn, info));
    }
}
