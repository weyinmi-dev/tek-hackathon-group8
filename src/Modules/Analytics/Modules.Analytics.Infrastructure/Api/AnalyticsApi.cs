using Modules.Analytics.Api;
using Modules.Analytics.Domain;
using Modules.Analytics.Domain.Audit;

namespace Modules.Analytics.Infrastructure.Api;

internal sealed class AnalyticsApi(IAuditRepository audit, IUnitOfWork uow) : IAnalyticsApi
{
    public async Task RecordAsync(string actor, string role, string action, string target, string sourceIp, CancellationToken ct = default)
    {
        await audit.AddAsync(AuditEntry.Record(actor, role, action, target, sourceIp), ct);
        await uow.SaveChangesAsync(ct);
    }
}
