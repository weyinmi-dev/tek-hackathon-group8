namespace Modules.Alerts.Domain.Alerts;

public enum AlertSeverity { Info = 0, Warn = 1, Critical = 2 }

public enum AlertStatus { Active = 0, Investigating = 1, Monitoring = 2, Acknowledged = 3, Resolved = 4 }

public static class AlertEnums
{
    public static string ToWire(this AlertSeverity s) => s switch
    {
        AlertSeverity.Critical => "critical",
        AlertSeverity.Warn => "warn",
        _ => "info"
    };

    public static string ToWire(this AlertStatus s) => s switch
    {
        AlertStatus.Active => "active",
        AlertStatus.Investigating => "investigating",
        AlertStatus.Monitoring => "monitoring",
        AlertStatus.Acknowledged => "acknowledged",
        _ => "resolved"
    };
}
