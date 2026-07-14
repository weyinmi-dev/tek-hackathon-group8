namespace Application.Abstractions.Pipeline;

/// <summary>
/// Pure conversions from OSS-reported physical measurements to the normalised units the
/// existing aggregates already speak (percentages, statuses). Kept in one place, free of
/// any dependency, so both the Stage-1 parser and the Stage-3 planner derive identically
/// and every mapping is unit-testable in isolation.
///
/// Every constant here is a documented modelling choice, not a fact from the feed. When a
/// vendor eventually reports one of these directly, prefer the reported value and delete
/// the derivation rather than blending the two.
/// </summary>
public static class SnapshotDerivations
{
    // RSRP is reported in dBm and is the standard RAN proxy for downlink signal quality.
    // The usable window in LTE/NR planning runs from roughly -120 dBm (cell edge, unusable)
    // to -70 dBm (excellent, close to the radio). Map that window linearly onto 0..100 and
    // clamp outside it. -91 dBm — the value in MTN's reference payload — lands at 58%.
    private const double RsrpFloorDbm = -120.0;
    private const double RsrpCeilingDbm = -70.0;

    // A 48 V telecom battery string sits near 54 V on float charge and is treated as fully
    // discharged at its 42 V low-voltage cutoff; below that the rectifier drops the load.
    private const double BatteryFloorVolts = 42.0;
    private const double BatteryCeilingVolts = 54.0;

    /// <summary>
    /// Downlink signal quality as a percentage, from RSRP in dBm. Returns null when the
    /// snapshot carried no RSRP KPI — the caller must not substitute a fabricated value.
    /// </summary>
    public static int? SignalPctFromRsrp(double? rsrpDbm)
    {
        if (rsrpDbm is not double rsrp)
        {
            return null;
        }

        double normalised = (rsrp - RsrpFloorDbm) / (RsrpCeilingDbm - RsrpFloorDbm) * 100.0;
        return (int)Math.Round(Math.Clamp(normalised, 0.0, 100.0));
    }

    /// <summary>State of charge as a percentage, from the DC bus voltage of a 48 V string.</summary>
    public static int? BatteryPctFromVoltage(double? volts)
    {
        if (volts is not double v)
        {
            return null;
        }

        double normalised = (v - BatteryFloorVolts) / (BatteryCeilingVolts - BatteryFloorVolts) * 100.0;
        return (int)Math.Round(Math.Clamp(normalised, 0.0, 100.0));
    }

    /// <summary>
    /// Looks a KPI up by name, case-insensitively. KPI names are vendor strings ("RSRP",
    /// "PRB Utilization"), so matching is tolerant of case and surrounding whitespace.
    /// </summary>
    public static double? Kpi(IReadOnlyList<SnapshotKpi>? kpis, string name)
    {
        if (kpis is null)
        {
            return null;
        }

        foreach (SnapshotKpi kpi in kpis)
        {
            if (string.Equals(kpi.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return kpi.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Wire-level tower status ("CRITICAL" / "WARN" / "OK") for a snapshot, from the site's
    /// own health score and the severity of the alarms it is currently reporting. An active
    /// Critical alarm outranks a flattering health score — a site on generator with a failed
    /// grid feed is not "Operational" no matter what number the OSS attaches to it.
    /// </summary>
    public static string TowerStatusFrom(int? healthScore, IReadOnlyList<SnapshotAlarm> alarms)
    {
        ArgumentNullException.ThrowIfNull(alarms);

        bool hasCritical = alarms.Any(a =>
            IsOpen(a) && string.Equals(a.Severity, "Critical", StringComparison.OrdinalIgnoreCase));
        if (hasCritical)
        {
            return "CRITICAL";
        }

        bool hasMajor = alarms.Any(a =>
            IsOpen(a) &&
            (string.Equals(a.Severity, "Major", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(a.Severity, "Minor", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(a.Severity, "Warning", StringComparison.OrdinalIgnoreCase)));
        if (hasMajor)
        {
            return "WARN";
        }

        return healthScore switch
        {
            < 50 => "CRITICAL",
            < 80 => "WARN",
            _ => "OK"
        };
    }

    /// <summary>
    /// An alarm still counts against the site unless it has been explicitly cleared or
    /// resolved upstream. "Acknowledged" means someone has seen it, not that it is gone.
    /// </summary>
    public static bool IsOpen(SnapshotAlarm alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);

        return alarm.Status is null || !ClearedStatuses.Contains(alarm.Status.Trim());
    }

    private static readonly HashSet<string> ClearedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Cleared", "Resolved", "Closed" };
}
