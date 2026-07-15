using SharedKernel;

namespace Modules.Energy.Domain.Events;

public enum AnomalyKind
{
    FuelTheft = 0,
    SensorOffline = 1,
    GenOveruse = 2,
    BatteryDegrade = 3,
    PredictedFault = 4,
}

public enum AnomalySeverity
{
    Info = 0,
    Warn = 1,
    Critical = 2,
}

public static class AnomalyExtensions
{
    public static string ToWire(this AnomalyKind k) => k switch
    {
        AnomalyKind.FuelTheft => "fuel-theft",
        AnomalyKind.SensorOffline => "sensor-offline",
        AnomalyKind.GenOveruse => "gen-overuse",
        AnomalyKind.BatteryDegrade => "battery-degrade",
        AnomalyKind.PredictedFault => "predicted-fault",
        _ => "fuel-theft",
    };

    public static string ToWire(this AnomalySeverity s) => s switch
    {
        AnomalySeverity.Critical => "critical",
        AnomalySeverity.Warn => "warn",
        _ => "info",
    };
}

/// <summary>
/// Detection produced by the anomaly engine (Isolation Forest in production; rule + statistical
/// thresholds in this demo). Acknowledge clears the open-anomaly flag on the parent Site so its
/// health rating can recover.
/// </summary>
public sealed class AnomalyEvent : Entity
{
    private AnomalyEvent(
        Guid id, string siteCode, AnomalyKind kind, AnomalySeverity severity,
        string detail, double confidence, string modelName, DateTime detectedAtUtc) : base(id)
    {
        SiteCode = siteCode;
        Kind = kind;
        Severity = severity;
        Detail = detail;
        Confidence = confidence;
        ModelName = modelName;
        DetectedAtUtc = detectedAtUtc;
    }

    private AnomalyEvent() { }

    public string SiteCode { get; private set; } = null!;
    public AnomalyKind Kind { get; private set; }
    public AnomalySeverity Severity { get; private set; }
    public string Detail { get; private set; } = null!;
    public double Confidence { get; private set; }
    public string ModelName { get; private set; } = null!;
    public DateTime DetectedAtUtc { get; private set; }
    public bool Acknowledged { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public string? AcknowledgedBy { get; private set; }

    /// <summary>
    /// Stable identity of the *condition*, not the sighting: <c>snapshot:{siteCode}:{kind}</c>.
    ///
    /// A site on a failing battery re-reports that fact on every 15-minute poll. Keyed on the
    /// condition, those collapse into one open anomaly that gets refreshed; keyed on the sighting
    /// they would pile up ninety-six rows a day and bury the anomalies that matter. Null on
    /// seeded and ML-detected rows, which do not participate in this dedup.
    /// </summary>
    public string? DetectionKey { get; private set; }

    public static AnomalyEvent Detect(
        string siteCode, AnomalyKind kind, AnomalySeverity severity,
        string detail, double confidence, string modelName) =>
        new(Guid.NewGuid(), siteCode, kind, severity, detail, confidence, modelName, DateTime.UtcNow);

    /// <summary>
    /// Detected by a rule over a reported snapshot rather than by the model. Stamped with the
    /// snapshot's own capture time, so a backfilled upload lands on the timeline where it belongs
    /// instead of appearing to have happened at import.
    /// </summary>
    public static AnomalyEvent DetectFromSnapshot(
        string siteCode, AnomalyKind kind, AnomalySeverity severity,
        string detail, double confidence, DateTime detectedAtUtc, string detectionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionKey);

        return new AnomalyEvent(
            Guid.NewGuid(), siteCode.Trim().ToUpperInvariant(), kind, severity, detail, confidence,
            modelName: SnapshotRuleModel,
            detectedAtUtc: DateTime.SpecifyKind(detectedAtUtc, DateTimeKind.Utc))
        {
            DetectionKey = detectionKey
        };
    }

    /// <summary>
    /// Named so nobody mistakes a threshold for a model. These rows sit in the same table as the
    /// detector's, and an operator reading "IsolationForest" against a rule-derived anomaly would be
    /// misled about how much to trust it.
    /// </summary>
    public const string SnapshotRuleModel = "snapshot-rules/v1";

    /// <summary>
    /// The condition is still being reported. Refreshes what we know about it; returns true when
    /// something actually changed, so an unchanged re-report is a no-op rather than a false update.
    /// </summary>
    public bool Observe(AnomalySeverity severity, string detail, double confidence, DateTime detectedAtUtc)
    {
        bool changed =
            Severity != severity ||
            !string.Equals(Detail, detail, StringComparison.Ordinal) ||
            Math.Abs(Confidence - confidence) > 0.001 ||
            Acknowledged;

        Severity = severity;
        Detail = detail;
        Confidence = confidence;
        DetectedAtUtc = DateTime.SpecifyKind(detectedAtUtc, DateTimeKind.Utc);

        // A condition that comes back after being acknowledged is open again. Leaving it
        // acknowledged would let a recurring fault hide behind a tick someone made yesterday.
        if (Acknowledged)
        {
            Acknowledged = false;
            AcknowledgedAtUtc = null;
            AcknowledgedBy = null;
        }

        return changed;
    }

    /// <summary>
    /// The condition has cleared — the latest snapshot no longer reports it. Closed by the system
    /// rather than by a person, and recorded as such so the audit trail does not credit an operator
    /// with an acknowledgement they never made.
    /// </summary>
    public bool AutoResolve(DateTime resolvedAtUtc)
    {
        if (Acknowledged)
        {
            return false;
        }

        Acknowledged = true;
        AcknowledgedAtUtc = DateTime.SpecifyKind(resolvedAtUtc, DateTimeKind.Utc);
        AcknowledgedBy = SystemActor;
        return true;
    }

    public const string SystemActor = "system:sync";

    public void Acknowledge(string actorHandle)
    {
        if (Acknowledged) return;
        Acknowledged = true;
        AcknowledgedAtUtc = DateTime.UtcNow;
        AcknowledgedBy = actorHandle;
    }

    /// <summary>The dedup key for a rule-detected condition at a site.</summary>
    public static string KeyFor(string siteCode, AnomalyKind kind) =>
        $"snapshot:{siteCode.Trim().ToUpperInvariant()}:{kind}";
}

public interface IAnomalyEventRepository
{
    Task AddAsync(AnomalyEvent ev, CancellationToken ct = default);
    Task<AnomalyEvent?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AnomalyEvent>> ListAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<AnomalyEvent>> ListOpenForSiteAsync(string siteCode, CancellationToken ct = default);

    /// <summary>
    /// Every rule-detected anomaly for a site, tracked for mutation.
    ///
    /// <see cref="ListOpenForSiteAsync"/> is a read (AsNoTracking) and returns only unacknowledged
    /// rows — right for display, wrong for synchronisation, which needs to reopen an acknowledged
    /// condition that has come back and to close one that has cleared.
    /// </summary>
    Task<IReadOnlyList<AnomalyEvent>> ListSnapshotDetectedForUpdateAsync(
        string siteCode, CancellationToken ct = default);
    Task<int> CountAsync(AnomalySeverity? minSeverity, bool openOnly, CancellationToken ct = default);
    Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
}
