namespace Modules.Energy.Domain.Sites;

public enum SiteHealth
{
    Ok = 0,
    Degraded = 1,
    Critical = 2,
}

public static class SiteHealthExtensions
{
    public static string ToWire(this SiteHealth h) => h switch
    {
        SiteHealth.Critical => "critical",
        SiteHealth.Degraded => "degraded",
        _ => "ok",
    };

    /// <summary>
    /// Health derivation rule, kept here so both domain mutations and the seeder
    /// agree on what "critical" means. A site is critical when fuel or battery is
    /// dangerously low; degraded when running off generator/battery for too long.
    /// </summary>
    public static SiteHealth Derive(int battPct, int dieselPct, PowerSource source, bool gridUp, bool hasOpenAnomaly)
    {
        if (battPct < 30 || dieselPct < 20) return SiteHealth.Critical;
        if (hasOpenAnomaly) return SiteHealth.Degraded;
        if (!gridUp && source != PowerSource.Solar) return SiteHealth.Degraded;
        return SiteHealth.Ok;
    }
}
