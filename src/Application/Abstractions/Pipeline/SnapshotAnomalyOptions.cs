namespace Application.Abstractions.Pipeline;

/// <summary>
/// Every threshold the snapshot anomaly rules use, bound from configuration.
///
/// These are policy, not physics. What counts as "too much fuel to have evaporated" or "a battery
/// too flat to ride out an outage" is an operational judgement that varies by fleet, by region, and
/// by how much false-positive noise a NOC is willing to tolerate — so it belongs in configuration
/// where an operator can change it, not baked into a constant only a developer can reach.
///
/// The defaults below are the ones the rules shipped with; they are a starting point, not an answer.
/// Every one of them is validated at startup (see <see cref="Validate"/>), because a threshold
/// silently misconfigured to zero would turn a rule into a firehose or switch it off entirely, and
/// either failure is invisible until someone notices the anomalies page is lying.
///
/// Each rule can also be switched off outright. A fleet with no generator telemetry has no business
/// being told its generators are overused.
/// </summary>
public sealed record SnapshotAnomalyOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "Ingestion:SnapshotAnomalies";

    public FuelTheftRuleOptions FuelTheft { get; init; } = new();
    public GeneratorOveruseRuleOptions GeneratorOveruse { get; init; } = new();
    public BatteryDegradeRuleOptions BatteryDegrade { get; init; } = new();
    public SensorOfflineRuleOptions SensorOffline { get; init; } = new();
    public FuelExhaustionRuleOptions FuelExhaustion { get; init; } = new();

    /// <summary>
    /// Throws if any threshold is nonsense. Called at startup so a bad edit to appsettings stops the
    /// app with a readable message, rather than quietly producing an anomalies page nobody can trust.
    /// </summary>
    public void Validate()
    {
        FuelTheft.Validate();
        GeneratorOveruse.Validate();
        BatteryDegrade.Validate();
        SensorOffline.Validate();
        FuelExhaustion.Validate();
    }

    internal static void EnsureConfidence(double value, string name)
    {
        if (value is <= 0 or > 1)
        {
            throw new InvalidOperationException(
                $"{SectionName}: {name} must be greater than 0 and at most 1, but was {value}.");
        }
    }

    internal static void EnsurePositive(double value, string name)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}: {name} must be greater than 0, but was {value}.");
        }
    }
}

/// <summary>
/// Fuel that leaves a tank the generator was not running to burn did not evaporate.
/// </summary>
public sealed record FuelTheftRuleOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Percentage points of fuel loss, between two consecutive readings with the generator idle in
    /// both, before we call it theft. Set it above the noise floor of your level senders — floats
    /// typically wobble by one to three points — or the rule will cry theft at every tanker bounce.
    /// </summary>
    public int DropPoints { get; init; } = 8;

    /// <summary>A loss this large is not ambiguous. Below it the anomaly is a warning.</summary>
    public int CriticalDropPoints { get; init; } = 20;

    /// <summary>Confidence floor for a drop that only just clears <see cref="DropPoints"/>.</summary>
    public double BaseConfidence { get; init; } = 0.6;

    /// <summary>Confidence added per point of loss. A bigger hole is a stronger claim.</summary>
    public double ConfidencePerPoint { get; init; } = 0.02;

    /// <summary>
    /// Ceiling on confidence. Deliberately below 1.0: a failed level sender could still explain the
    /// reading, and a rule must never present itself as a certainty.
    /// </summary>
    public double MaxConfidence { get; init; } = 0.95;

    public void Validate()
    {
        if (DropPoints is <= 0 or > 100)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: FuelTheft.DropPoints must be between 1 and 100, but was {DropPoints}.");
        }

        if (CriticalDropPoints < DropPoints)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: FuelTheft.CriticalDropPoints ({CriticalDropPoints}) " +
                $"cannot be below DropPoints ({DropPoints}) — nothing would ever be merely a warning.");
        }

        SnapshotAnomalyOptions.EnsureConfidence(BaseConfidence, "FuelTheft.BaseConfidence");
        SnapshotAnomalyOptions.EnsureConfidence(MaxConfidence, "FuelTheft.MaxConfidence");

        if (MaxConfidence < BaseConfidence)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: FuelTheft.MaxConfidence ({MaxConfidence}) " +
                $"cannot be below BaseConfidence ({BaseConfidence}).");
        }

        if (ConfidencePerPoint < 0)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: FuelTheft.ConfidencePerPoint cannot be negative.");
        }
    }
}

/// <summary>Burning diesel for load the grid is already able to carry.</summary>
public sealed record GeneratorOveruseRuleOptions
{
    public bool Enabled { get; init; } = true;

    public double Confidence { get; init; } = 0.9;

    public void Validate() => SnapshotAnomalyOptions.EnsureConfidence(Confidence, "GeneratorOveruse.Confidence");
}

/// <summary>A battery too flat to carry the site through an outage.</summary>
public sealed record BatteryDegradeRuleOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>State of charge below which the bank is a problem.</summary>
    public int WarnBelowPct { get; init; } = 30;

    /// <summary>State of charge below which it will not ride out anything at all.</summary>
    public int CriticalBelowPct { get; init; } = 15;

    public double Confidence { get; init; } = 0.95;

    public void Validate()
    {
        if (WarnBelowPct is <= 0 or > 100)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: BatteryDegrade.WarnBelowPct must be between 1 and 100, but was {WarnBelowPct}.");
        }

        if (CriticalBelowPct >= WarnBelowPct)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: BatteryDegrade.CriticalBelowPct ({CriticalBelowPct}) " +
                $"must be below WarnBelowPct ({WarnBelowPct}) — otherwise nothing is ever merely a warning.");
        }

        SnapshotAnomalyOptions.EnsureConfidence(Confidence, "BatteryDegrade.Confidence");
    }
}

/// <summary>
/// A site that has stopped speaking. Its last readings are only as fresh as the link that carried
/// them, so a stale heartbeat outranks whatever the rest of the document claims.
/// </summary>
public sealed record SensorOfflineRuleOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Minutes of silence before the site counts as not reporting. Set this comfortably above your
    /// feed's polling interval — a 15-minute feed will always be a few minutes stale, and a threshold
    /// under the interval would flag every healthy site in the fleet.
    /// </summary>
    public int StaleMinutes { get; init; } = 30;

    public int CriticalStaleMinutes { get; init; } = 120;

    public double Confidence { get; init; } = 0.9;

    public void Validate()
    {
        SnapshotAnomalyOptions.EnsurePositive(StaleMinutes, "SensorOffline.StaleMinutes");

        if (CriticalStaleMinutes < StaleMinutes)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: SensorOffline.CriticalStaleMinutes ({CriticalStaleMinutes}) " +
                $"cannot be below StaleMinutes ({StaleMinutes}).");
        }

        SnapshotAnomalyOptions.EnsureConfidence(Confidence, "SensorOffline.Confidence");
    }
}

/// <summary>
/// Straight-line projection of the generator running out of fuel, from the burn rate measured
/// between the last two readings.
/// </summary>
public sealed record FuelExhaustionRuleOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Warn when the tank is projected to run dry inside this many hours. Set it to your realistic
    /// refuelling lead time — a warning that arrives after the truck could no longer have got there
    /// is not a warning, it is a postmortem.
    /// </summary>
    public double WarningHours { get; init; } = 12.0;

    public double CriticalHours { get; init; } = 4.0;

    /// <summary>
    /// A straight line through two points. Honest, and not much more than that — which is why this
    /// is the lowest confidence of any rule.
    /// </summary>
    public double Confidence { get; init; } = 0.7;

    public void Validate()
    {
        SnapshotAnomalyOptions.EnsurePositive(WarningHours, "FuelExhaustion.WarningHours");

        if (CriticalHours > WarningHours)
        {
            throw new InvalidOperationException(
                $"{SnapshotAnomalyOptions.SectionName}: FuelExhaustion.CriticalHours ({CriticalHours}) " +
                $"cannot exceed WarningHours ({WarningHours}) — a nearer deadline cannot be the less urgent one.");
        }

        SnapshotAnomalyOptions.EnsureConfidence(Confidence, "FuelExhaustion.Confidence");
    }
}
