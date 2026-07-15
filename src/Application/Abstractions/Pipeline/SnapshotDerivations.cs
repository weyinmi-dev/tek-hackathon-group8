namespace Application.Abstractions.Pipeline;

/// <summary>
/// Pure conversions from OSS-reported physical measurements to the normalised units the existing
/// aggregates already speak (percentages, statuses). Kept in one place, free of any dependency, so
/// every caller derives identically and every mapping is unit-testable in isolation.
///
/// Nothing here carries a constant of its own. Every window and threshold arrives as
/// <see cref="SnapshotCalibrationOptions"/>, bound from configuration and validated at startup —
/// these are characteristics of a fleet's hardware and a network's planning, not universal truths,
/// and a 48 V window silently applied to a 24 V bank would report every healthy battery as flat.
/// </summary>
public static class SnapshotDerivations
{
    /// <summary>
    /// Downlink signal quality as a percentage, from RSRP in dBm, mapped linearly across the
    /// configured planning window and clamped outside it. Returns null when the snapshot carried no
    /// RSRP KPI — the caller must not substitute a fabricated value.
    /// </summary>
    public static int? SignalPctFromRsrp(double? rsrpDbm, SnapshotCalibrationOptions calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        if (rsrpDbm is not double rsrp)
        {
            return null;
        }

        RsrpCalibration window = calibration.Rsrp;
        return Interpolate(rsrp, window.FloorDbm, window.CeilingDbm);
    }

    /// <summary>State of charge as a percentage, from the DC bus voltage of the configured string.</summary>
    public static int? BatteryPctFromVoltage(double? volts, SnapshotCalibrationOptions calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        if (volts is not double v)
        {
            return null;
        }

        BatteryCalibration window = calibration.Battery;
        return Interpolate(v, window.FloorVolts, window.CeilingVolts);
    }

    private static int Interpolate(double value, double atZero, double atHundred)
    {
        double normalised = (value - atZero) / (atHundred - atZero) * 100.0;
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
    /// Wire-level tower status ("CRITICAL" / "WARN" / "OK") for a snapshot, from the site's own health
    /// score and the severity of the alarms it is currently reporting. An active Critical alarm
    /// outranks a flattering health score — a site on generator with a failed grid feed is not
    /// "Operational" no matter what number the OSS attaches to it.
    /// </summary>
    public static string TowerStatusFrom(
        int? healthScore,
        IReadOnlyList<SnapshotAlarm> alarms,
        SnapshotCalibrationOptions calibration)
    {
        ArgumentNullException.ThrowIfNull(alarms);
        ArgumentNullException.ThrowIfNull(calibration);

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

        if (healthScore is not int score)
        {
            return "OK";
        }

        HealthScoreCalibration thresholds = calibration.HealthScore;

        if (score < thresholds.CriticalBelow)
        {
            return "CRITICAL";
        }

        return score < thresholds.WarnBelow ? "WARN" : "OK";
    }

    /// <summary>
    /// An alarm still counts against the site unless it has been explicitly cleared or resolved
    /// upstream. "Acknowledged" means someone has seen it, not that it is gone.
    /// </summary>
    public static bool IsOpen(SnapshotAlarm alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);

        return alarm.Status is null || !ClearedStatuses.Contains(alarm.Status.Trim());
    }

    private static readonly HashSet<string> ClearedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Cleared", "Resolved", "Closed" };
}
