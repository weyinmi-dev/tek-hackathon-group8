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

    public static AnomalyEvent Detect(
        string siteCode, AnomalyKind kind, AnomalySeverity severity,
        string detail, double confidence, string modelName) =>
        new(Guid.NewGuid(), siteCode, kind, severity, detail, confidence, modelName, DateTime.UtcNow);

    public void Acknowledge(string actorHandle)
    {
        if (Acknowledged) return;
        Acknowledged = true;
        AcknowledgedAtUtc = DateTime.UtcNow;
        AcknowledgedBy = actorHandle;
    }
}

public interface IAnomalyEventRepository
{
    Task AddAsync(AnomalyEvent ev, CancellationToken ct = default);
    Task<AnomalyEvent?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AnomalyEvent>> ListAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<AnomalyEvent>> ListOpenForSiteAsync(string siteCode, CancellationToken ct = default);
    Task<int> CountAsync(AnomalySeverity? minSeverity, bool openOnly, CancellationToken ct = default);
    Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
}
