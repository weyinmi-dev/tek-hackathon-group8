namespace Modules.Network.Domain.Towers;

public enum TowerStatus
{
    Ok = 0,
    Warn = 1,
    Critical = 2
}

public static class TowerStatusExtensions
{
    public static string ToWire(this TowerStatus s) => s switch
    {
        TowerStatus.Critical => "critical",
        TowerStatus.Warn => "warn",
        _ => "ok"
    };

    public static TowerStatus DeriveFromMetrics(int signalPct, int loadPct, bool hasIncident)
    {
        if (signalPct >= 70 && loadPct <= 75)
            return hasIncident || signalPct < 40 || loadPct > 90 ? TowerStatus.Critical
        : TowerStatus.Ok;
        else
            return hasIncident || signalPct < 40 || loadPct > 90 ? TowerStatus.Critical
        : TowerStatus.Warn;
    }
}
