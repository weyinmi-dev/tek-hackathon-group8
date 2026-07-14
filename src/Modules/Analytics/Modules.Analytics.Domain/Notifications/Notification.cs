using SharedKernel;

namespace Modules.Analytics.Domain.Notifications;

public enum NotificationKind
{
    CriticalAlarm = 0,
    UploadCompleted = 1,
    SynchronizationFailed = 2,
    HealthDegraded = 3,
    PredictionChanged = 4
}

public enum NotificationSeverity
{
    Info = 0,
    Warn = 1,
    Critical = 2
}

/// <summary>
/// One thing an operator should know about. Lives in Analytics because that module already owns the
/// cross-cutting read models (audit, ingestion dashboard) — a notification is another projection of
/// what happened elsewhere, not a new bounded context.
///
/// Deliberately not addressed to a user. The pre-existing <c>INotificationService.SendAsync</c> takes
/// a userId, but everything worth notifying here is a NOC-wide fact — a site is on fire, an upload
/// failed — and inventing a recipient for it would be fiction. The feed is broadcast; read state is
/// per-notification, which is enough for a single NOC and honest about what we actually know.
/// </summary>
public sealed class Notification : Entity
{
    private Notification(
        Guid id,
        NotificationKind kind,
        NotificationSeverity severity,
        string title,
        string body,
        string? siteCode,
        string? link,
        string? dedupeKey,
        DateTime raisedAtUtc) : base(id)
    {
        Kind = kind;
        Severity = severity;
        Title = title;
        Body = body;
        SiteCode = siteCode;
        Link = link;
        DedupeKey = dedupeKey;
        RaisedAtUtc = raisedAtUtc;
    }

    private Notification() { }

    public NotificationKind Kind { get; private set; }
    public NotificationSeverity Severity { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string? SiteCode { get; private set; }

    /// <summary>Where clicking it should take the operator, e.g. "/sites/LAG0456".</summary>
    public string? Link { get; private set; }

    /// <summary>
    /// Stable key for "this is the same news". The pipeline is at-least-once and an OSS feed re-reports
    /// a standing alarm on every poll; without this, a site that has been on generator for an hour
    /// would produce a notification every fifteen minutes and bury everything else.
    /// </summary>
    public string? DedupeKey { get; private set; }

    public DateTime RaisedAtUtc { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public static Notification Raise(
        NotificationKind kind,
        NotificationSeverity severity,
        string title,
        string body,
        string? siteCode = null,
        string? link = null,
        string? dedupeKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Notification(
            Guid.NewGuid(), kind, severity, title, body ?? string.Empty,
            siteCode, link, dedupeKey, DateTime.UtcNow);
    }

    public bool MarkRead()
    {
        if (IsRead)
        {
            return false;
        }

        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
        return true;
    }
}

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> ListAsync(bool unreadOnly, int take, CancellationToken ct = default);
    Task<Notification?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListUnreadAsync(CancellationToken ct = default);
    Task<int> CountUnreadAsync(CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>
    /// True when an unread notification with this dedupe key already exists. Checked against unread
    /// only: once an operator has seen and dismissed it, a fresh recurrence is news again.
    /// </summary>
    Task<bool> ExistsUnreadAsync(string dedupeKey, CancellationToken ct = default);
}
