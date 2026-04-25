using SharedKernel;

namespace Modules.Analytics.Domain.Audit;

public sealed class AuditEntry : Entity
{
    private AuditEntry(Guid id, DateTime occurredAtUtc, string actor, string role, string action, string target, string sourceIp) : base(id)
    {
        OccurredAtUtc = occurredAtUtc;
        Actor = actor;
        Role = role;
        Action = action;
        Target = target;
        SourceIp = sourceIp;
    }

    private AuditEntry() { }

    public DateTime OccurredAtUtc { get; private set; }
    public string Actor { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Target { get; private set; } = null!;
    public string SourceIp { get; private set; } = null!;

    public static AuditEntry Record(string actor, string role, string action, string target, string sourceIp) =>
        new(Guid.NewGuid(), DateTime.UtcNow, actor, role, action, target, sourceIp);

    public static AuditEntry RecordAt(DateTime atUtc, string actor, string role, string action, string target, string sourceIp) =>
        new(Guid.NewGuid(), atUtc, actor, role, action, target, sourceIp);
}

public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default);
}
