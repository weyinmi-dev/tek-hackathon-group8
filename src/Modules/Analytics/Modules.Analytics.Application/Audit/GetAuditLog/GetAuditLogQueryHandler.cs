using Application.Abstractions.Messaging;
using Modules.Analytics.Domain.Audit;
using SharedKernel;

namespace Modules.Analytics.Application.Audit.GetAuditLog;

internal sealed class GetAuditLogQueryHandler(IAuditRepository audit)
    : IQueryHandler<GetAuditLogQuery, IReadOnlyList<AuditEntryDto>>
{
    public async Task<Result<IReadOnlyList<AuditEntryDto>>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<AuditEntry> rows = await audit.ListRecentAsync(request.Take, cancellationToken);
        IReadOnlyList<AuditEntryDto> dtos = rows
            .Select(e => new AuditEntryDto(
                e.OccurredAtUtc.ToString("HH:mm:ss"),
                e.Actor,
                e.Role,
                e.Action,
                e.Target,
                e.SourceIp))
            .ToList();
        return Result.Success(dtos);
    }
}
