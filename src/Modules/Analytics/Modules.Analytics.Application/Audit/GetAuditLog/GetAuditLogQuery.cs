using Application.Abstractions.Messaging;

namespace Modules.Analytics.Application.Audit.GetAuditLog;

public sealed record GetAuditLogQuery(int Take = 50) : IQuery<IReadOnlyList<AuditEntryDto>>;

public sealed record AuditEntryDto(string Time, string Actor, string Role, string Action, string Target, string Ip);
