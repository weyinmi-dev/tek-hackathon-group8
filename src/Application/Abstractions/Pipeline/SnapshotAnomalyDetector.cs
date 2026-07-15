namespace Application.Abstractions.Pipeline;

/// <summary>
/// Derives energy anomalies from what a site snapshot reports, comparing it against the site's
/// previous snapshot where a rule needs a rate of change.
///
/// Pure: same snapshots and same options in, same anomalies out. No clock, no I/O — the "now" of a
/// detection is the snapshot's own capture time, so replaying an old upload produces the detections
/// it would have produced then, not the ones today's wall clock would imply.
///
/// Every rule is a *rule*, not an inference — each is a statement about physics or arithmetic that an
/// operator could check by hand. That matters because these feed the same AnomalyEvent table the ML
/// detector writes to, and a fabricated confidence score sitting next to a real one is worse than no
/// score at all. Nothing here claims to be a model.
///
/// Not a single threshold is hardcoded. Every number comes from <see cref="SnapshotAnomalyOptions"/>,
/// bound from configuration and validated at startup — what counts as "too much fuel to have
/// evaporated" is an operational judgement that belongs to whoever runs the fleet, not to whoever
/// wrote this file.
/// </summary>
public static class SnapshotAnomalyDetector
{
    public static IReadOnlyList<DetectedEnergyAnomaly> Detect(
        SiteSnapshotPayload current,
        SiteSnapshotPayload? previous,
        SnapshotAnomalyOptions options,

        /// <summary>
        /// Needed because the battery rule's thresholds are a percentage, and the scale that
        /// percentage is measured against is itself configurable. "Below 30%" is meaningless without
        /// knowing which voltage window defines 30%.
        /// </summary>
        SnapshotCalibrationOptions calibration)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(calibration);

        SnapshotEnvironmentalMetrics? env = current.Environmental;
        if (env is null)
        {
            // No plant readings at all. There is nothing to reason about, and inventing a "sensor
            // offline" from a feed that simply doesn't carry an environmental block would fire on
            // every RAN-only snapshot.
            return [];
        }

        DateTimeOffset observedAt = current.Performance?.CapturedAt ?? current.GeneratedAt;
        SnapshotEnvironmentalMetrics? prevEnv = previous?.Environmental;
        var found = new List<DetectedEnergyAnomaly>();

        // ── Fuel theft ──────────────────────────────────────────────────────────
        // Fuel fell while the generator was off, across both readings. It cannot have been burned.
        FuelTheftRuleOptions theft = options.FuelTheft;
        if (theft.Enabled &&
            env.GeneratorFuelPercent is int fuelNow &&
            prevEnv?.GeneratorFuelPercent is int fuelBefore &&
            env.GeneratorRunning != true &&
            prevEnv.GeneratorRunning != true)
        {
            int drop = fuelBefore - fuelNow;
            if (drop >= theft.DropPoints)
            {
                found.Add(new DetectedEnergyAnomaly(
                    Kind: EnergyAnomalyKind.FuelTheft,
                    Severity: drop >= theft.CriticalDropPoints
                        ? EnergyAnomalySeverity.Critical
                        : EnergyAnomalySeverity.Warn,
                    Detail: $"Generator fuel fell {drop} points ({fuelBefore}% → {fuelNow}%) with the " +
                            "generator not running. Fuel cannot be burned by an idle generator.",
                    Confidence: Math.Min(
                        theft.MaxConfidence,
                        theft.BaseConfidence + (drop * theft.ConfidencePerPoint)),
                    ObservedAt: observedAt));
            }
        }

        // ── Generator overuse ───────────────────────────────────────────────────
        GeneratorOveruseRuleOptions overuse = options.GeneratorOveruse;
        if (overuse.Enabled && env.GeneratorRunning == true && env.MainPowerAvailable == true)
        {
            found.Add(new DetectedEnergyAnomaly(
                Kind: EnergyAnomalyKind.GenOveruse,
                Severity: EnergyAnomalySeverity.Warn,
                Detail: "Generator is running while commercial power is available — diesel is being " +
                        "burned for load the grid is already able to carry.",
                Confidence: overuse.Confidence,
                ObservedAt: observedAt));
        }

        // ── Battery degradation ─────────────────────────────────────────────────
        BatteryDegradeRuleOptions battery = options.BatteryDegrade;
        if (battery.Enabled &&
            SnapshotDerivations.BatteryPctFromVoltage(env.BatteryVoltage, calibration) is int battPct &&
            battPct < battery.WarnBelowPct)
        {
            found.Add(new DetectedEnergyAnomaly(
                Kind: EnergyAnomalyKind.BatteryDegrade,
                Severity: battPct < battery.CriticalBelowPct
                    ? EnergyAnomalySeverity.Critical
                    : EnergyAnomalySeverity.Warn,
                Detail: $"Battery bank at {battPct}% ({env.BatteryVoltage:0.0} V). The site cannot ride " +
                        "through an outage at this state of charge.",
                Confidence: battery.Confidence,
                ObservedAt: observedAt));
        }

        // ── Sensor / site offline ───────────────────────────────────────────────
        SensorOfflineRuleOptions offline = options.SensorOffline;
        if (offline.Enabled && current.Site.LastHeartbeat is DateTimeOffset heartbeat)
        {
            double staleMinutes = (current.GeneratedAt - heartbeat).TotalMinutes;
            if (staleMinutes >= offline.StaleMinutes)
            {
                found.Add(new DetectedEnergyAnomaly(
                    Kind: EnergyAnomalyKind.SensorOffline,
                    Severity: staleMinutes >= offline.CriticalStaleMinutes
                        ? EnergyAnomalySeverity.Critical
                        : EnergyAnomalySeverity.Warn,
                    Detail: $"No heartbeat for {(int)staleMinutes} minutes. The readings in this " +
                            "snapshot may not reflect the site's current state.",
                    Confidence: offline.Confidence,
                    ObservedAt: observedAt));
            }
        }

        // ── Predicted fuel exhaustion ───────────────────────────────────────────
        // Straight-line projection from the burn rate between the last two snapshots. Only meaningful
        // while the generator is actually running and actually consuming.
        FuelExhaustionRuleOptions exhaustion = options.FuelExhaustion;
        if (exhaustion.Enabled &&
            env.GeneratorRunning == true &&
            env.GeneratorFuelPercent is int fuelPct &&
            prevEnv?.GeneratorFuelPercent is int prevFuelPct &&
            previous is not null)
        {
            DateTimeOffset prevAt = previous.Performance?.CapturedAt ?? previous.GeneratedAt;
            double hours = (observedAt - prevAt).TotalHours;
            int burned = prevFuelPct - fuelPct;

            if (hours > 0 && burned > 0)
            {
                double burnPerHour = burned / hours;
                double hoursToDry = fuelPct / burnPerHour;

                if (hoursToDry <= exhaustion.WarningHours)
                {
                    found.Add(new DetectedEnergyAnomaly(
                        Kind: EnergyAnomalyKind.PredictedFault,
                        Severity: hoursToDry <= exhaustion.CriticalHours
                            ? EnergyAnomalySeverity.Critical
                            : EnergyAnomalySeverity.Warn,
                        Detail: $"Generator fuel at {fuelPct}%, burning {burnPerHour:0.0} points/hour — " +
                                $"projected dry in {hoursToDry:0.0} hours. Refuel before the site drops.",
                        Confidence: exhaustion.Confidence,
                        ObservedAt: observedAt));
                }
            }
        }

        return found;
    }
}

/// <summary>
/// One anomaly a snapshot implies. Mirrors Energy's AnomalyKind/AnomalySeverity as wire values so the
/// shared pipeline contract stays free of Energy.Domain types.
/// </summary>
public sealed record DetectedEnergyAnomaly(
    EnergyAnomalyKind Kind,
    EnergyAnomalySeverity Severity,
    string Detail,
    double Confidence,
    DateTimeOffset ObservedAt);

public enum EnergyAnomalyKind
{
    FuelTheft = 0,
    SensorOffline = 1,
    GenOveruse = 2,
    BatteryDegrade = 3,
    PredictedFault = 4
}

public enum EnergyAnomalySeverity
{
    Info = 0,
    Warn = 1,
    Critical = 2
}
