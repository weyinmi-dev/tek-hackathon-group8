using SharedKernel;

namespace Modules.Network.Domain.Maintenance;

public enum MaintenanceTicketStatus
{
    /// <summary>Reported in the snapshot's open-tickets list.</summary>
    Open = 0,

    /// <summary>Reported in the snapshot's completed-work history.</summary>
    Completed = 1,

    /// <summary>
    /// Was open, then stopped being reported entirely — neither open nor completed. The provider
    /// dropped it without telling us how it ended, so we archive rather than claim it was fixed.
    /// </summary>
    Archived = 2
}

/// <summary>
/// A maintenance job at a site, identified by the provider's own <see cref="TicketId"/>.
///
/// The ticket's life is driven entirely by where it appears in successive snapshots:
///   in <c>openTickets</c>            → Open
///   in <c>maintenanceHistory</c>     → Completed (with the action taken)
///   in neither, having been Open     → Archived
///
/// That last rule is the conservative one. A ticket vanishing from the feed is not evidence the
/// work was done — inferring completion would silently close jobs that were actually cancelled or
/// lost, so it is archived with the fact recorded and nothing invented.
/// </summary>
public sealed class MaintenanceTicket : Entity
{
    private MaintenanceTicket(
        Guid id,
        string siteCode,
        string ticketId,
        MaintenanceTicketStatus status,
        string? priority,
        string? issue,
        string? providerStatus,
        string? assignedEngineerId,
        string? assignedEngineerName,
        DateTimeOffset? createdAt,
        DateTimeOffset? estimatedArrival,
        DateTime firstSeenAtUtc) : base(id)
    {
        SiteCode = siteCode;
        TicketId = ticketId;
        Status = status;
        Priority = priority;
        Issue = issue;
        ProviderStatus = providerStatus;
        AssignedEngineerId = assignedEngineerId;
        AssignedEngineerName = assignedEngineerName;
        CreatedAt = createdAt;
        EstimatedArrival = estimatedArrival;
        FirstSeenAtUtc = firstSeenAtUtc;
        LastSeenAtUtc = firstSeenAtUtc;
    }

    private MaintenanceTicket() { }

    public string SiteCode { get; private set; } = null!;

    /// <summary>The provider's identifier ("TT-20491"). The idempotency key for synchronisation.</summary>
    public string TicketId { get; private set; } = null!;

    public MaintenanceTicketStatus Status { get; private set; }
    public string? Priority { get; private set; }
    public string? Issue { get; private set; }

    /// <summary>The provider's own status string ("Assigned", "In Progress"). Kept verbatim; vendors differ.</summary>
    public string? ProviderStatus { get; private set; }

    public string? AssignedEngineerId { get; private set; }

    /// <summary>
    /// The engineer's name as the feed gave it. Held here rather than only as a link because
    /// completed-work history names an engineer without an id — see <see cref="Engineer"/>.
    /// </summary>
    public string? AssignedEngineerName { get; private set; }

    public DateTimeOffset? CreatedAt { get; private set; }
    public DateTimeOffset? EstimatedArrival { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>What was actually done ("Replaced battery bank"). Only set once completed.</summary>
    public string? CompletedAction { get; private set; }

    public DateTime FirstSeenAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    public static MaintenanceTicket Open(
        string siteCode,
        string ticketId,
        string? priority,
        string? providerStatus,
        string? issue,
        string? assignedEngineerId,
        string? assignedEngineerName,
        DateTimeOffset? createdAt,
        DateTimeOffset? estimatedArrival,
        DateTime seenAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

        return new MaintenanceTicket(
            Guid.NewGuid(),
            siteCode.Trim().ToUpperInvariant(),
            ticketId.Trim(),
            MaintenanceTicketStatus.Open,
            priority,
            issue,
            providerStatus,
            assignedEngineerId,
            assignedEngineerName,
            createdAt,
            estimatedArrival,
            seenAtUtc);
    }

    /// <summary>Re-reports an open ticket from a fresh snapshot. True when the record actually changed.</summary>
    public bool ObserveOpen(
        string? priority,
        string? providerStatus,
        string? issue,
        string? assignedEngineerId,
        string? assignedEngineerName,
        DateTimeOffset? estimatedArrival,
        DateTime seenAtUtc)
    {
        bool changed =
            Status != MaintenanceTicketStatus.Open ||
            !string.Equals(Priority, priority, StringComparison.Ordinal) ||
            !string.Equals(ProviderStatus, providerStatus, StringComparison.Ordinal) ||
            !string.Equals(Issue, issue, StringComparison.Ordinal) ||
            !string.Equals(AssignedEngineerId, assignedEngineerId, StringComparison.Ordinal) ||
            EstimatedArrival != estimatedArrival;

        Status = MaintenanceTicketStatus.Open;
        Priority = priority;
        ProviderStatus = providerStatus;
        Issue = issue;
        AssignedEngineerId = assignedEngineerId;
        AssignedEngineerName = assignedEngineerName;
        EstimatedArrival = estimatedArrival;
        LastSeenAtUtc = seenAtUtc;
        ArchivedAtUtc = null;

        return changed;
    }

    /// <summary>
    /// The ticket appeared in the snapshot's completed-work history. Idempotent: re-reporting the
    /// same completion is a no-op.
    /// </summary>
    public bool Complete(DateTimeOffset? completedAt, string? engineerName, string? action, DateTime seenAtUtc)
    {
        bool changed =
            Status != MaintenanceTicketStatus.Completed ||
            CompletedAt != completedAt ||
            !string.Equals(CompletedAction, action, StringComparison.Ordinal);

        Status = MaintenanceTicketStatus.Completed;
        CompletedAt = completedAt;
        CompletedAction = action;
        LastSeenAtUtc = seenAtUtc;
        ArchivedAtUtc = null;

        if (!string.IsNullOrWhiteSpace(engineerName))
        {
            AssignedEngineerName = engineerName;
        }

        return changed;
    }

    /// <summary>Soft-archive: the ticket was open and has now dropped out of the feed entirely.</summary>
    public bool Archive(DateTime archivedAtUtc)
    {
        if (Status != MaintenanceTicketStatus.Open)
        {
            return false;
        }

        Status = MaintenanceTicketStatus.Archived;
        ArchivedAtUtc = archivedAtUtc;
        return true;
    }
}

public interface IMaintenanceTicketRepository
{
    Task<IReadOnlyList<MaintenanceTicket>> ListForSiteAsync(string siteCode, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceTicket>> ListOpenAsync(int take, CancellationToken ct = default);
    Task AddAsync(MaintenanceTicket ticket, CancellationToken ct = default);
    Task<int> CountAsync(MaintenanceTicketStatus? status, CancellationToken ct = default);
}
