namespace Application.Abstractions.Pipeline;

/// <summary>
/// How a reported physical measurement is converted into the normalised units the aggregates speak.
///
/// These are plant and radio characteristics, not universal constants: a 24 V battery string, a
/// different rectifier cutoff, or a network planned to a different cell-edge RSRP all move these
/// numbers. Baking them in would mean a fleet with different hardware silently reads its own
/// telemetry wrong — a 48 V window applied to a 24 V bank reports every healthy battery as flat.
///
/// The battery window in particular is the input to the battery-degrade anomaly rule. Its thresholds
/// are configurable, so the scale they are measured against has to be too, or "below 30%" means
/// whatever this file happened to decide it meant.
/// </summary>
public sealed record SnapshotCalibrationOptions
{
    public const string SectionName = "Ingestion:SnapshotCalibration";

    public RsrpCalibration Rsrp { get; init; } = new();
    public BatteryCalibration Battery { get; init; } = new();
    public HealthScoreCalibration HealthScore { get; init; } = new();

    public void Validate()
    {
        Rsrp.Validate();
        Battery.Validate();
        HealthScore.Validate();
    }
}

/// <summary>
/// The RSRP window, in dBm, that maps onto 0–100% signal. Defaults are the usual LTE/NR planning
/// range: cell edge to close-to-the-radio.
/// </summary>
public sealed record RsrpCalibration
{
    /// <summary>dBm that reads as 0% — cell edge, unusable.</summary>
    public double FloorDbm { get; init; } = -120.0;

    /// <summary>dBm that reads as 100% — excellent.</summary>
    public double CeilingDbm { get; init; } = -70.0;

    public void Validate()
    {
        if (CeilingDbm <= FloorDbm)
        {
            throw new InvalidOperationException(
                $"{SnapshotCalibrationOptions.SectionName}: Rsrp.CeilingDbm ({CeilingDbm}) must be above " +
                $"Rsrp.FloorDbm ({FloorDbm}) — signal quality improves as RSRP rises.");
        }
    }
}

/// <summary>
/// The DC bus voltage window that maps onto 0–100% state of charge. Defaults describe a 48 V telecom
/// string: fully discharged at its low-voltage cutoff, near float charge at the top.
/// </summary>
public sealed record BatteryCalibration
{
    /// <summary>Volts that read as 0% — the low-voltage cutoff, below which the rectifier drops load.</summary>
    public double FloorVolts { get; init; } = 42.0;

    /// <summary>Volts that read as 100% — float charge.</summary>
    public double CeilingVolts { get; init; } = 54.0;

    public void Validate()
    {
        if (FloorVolts <= 0)
        {
            throw new InvalidOperationException(
                $"{SnapshotCalibrationOptions.SectionName}: Battery.FloorVolts must be positive, but was {FloorVolts}.");
        }

        if (CeilingVolts <= FloorVolts)
        {
            throw new InvalidOperationException(
                $"{SnapshotCalibrationOptions.SectionName}: Battery.CeilingVolts ({CeilingVolts}) must be above " +
                $"Battery.FloorVolts ({FloorVolts}).");
        }
    }
}

/// <summary>
/// Where a provider's own health score crosses into warning and critical, when no open alarm has
/// already settled the question. An open Critical alarm always outranks the score.
/// </summary>
public sealed record HealthScoreCalibration
{
    public int CriticalBelow { get; init; } = 50;
    public int WarnBelow { get; init; } = 80;

    public void Validate()
    {
        if (WarnBelow is <= 0 or > 100)
        {
            throw new InvalidOperationException(
                $"{SnapshotCalibrationOptions.SectionName}: HealthScore.WarnBelow must be between 1 and 100, " +
                $"but was {WarnBelow}.");
        }

        if (CriticalBelow >= WarnBelow)
        {
            throw new InvalidOperationException(
                $"{SnapshotCalibrationOptions.SectionName}: HealthScore.CriticalBelow ({CriticalBelow}) must be " +
                $"below HealthScore.WarnBelow ({WarnBelow}) — otherwise nothing is ever merely a warning.");
        }
    }
}
