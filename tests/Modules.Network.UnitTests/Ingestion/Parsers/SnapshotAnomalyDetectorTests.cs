using Application.Abstractions.Pipeline;
using FluentAssertions;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

/// <summary>
/// The anomaly rules are the one place synchronisation makes a *claim* rather than recording a fact,
/// so each one is pinned: the condition that fires it, and — just as importantly — the near miss that
/// must not.
/// </summary>
public sealed class SnapshotAnomalyDetectorTests
{
    /// <summary>The shipped defaults. Individual tests override a rule when they are testing tuning.</summary>
    private static readonly SnapshotAnomalyOptions Anomalies = new();
    private static readonly SnapshotCalibrationOptions Calibration = new();
    [Fact]
    public void FuelFallingWhileTheGeneratorIsOff_IsTheft()
    {
        SiteSnapshotPayload before = Snapshot(fuel: 80, generatorRunning: false, at: "2026-07-14T12:00:00Z");
        SiteSnapshotPayload now = Snapshot(fuel: 55, generatorRunning: false, at: "2026-07-14T12:15:00Z");

        IReadOnlyList<DetectedEnergyAnomaly> found = SnapshotAnomalyDetector.Detect(now, before, Anomalies, Calibration);

        DetectedEnergyAnomaly theft = found.Single(a => a.Kind == EnergyAnomalyKind.FuelTheft);
        theft.Severity.Should().Be(EnergyAnomalySeverity.Critical, "a 25-point loss is far past a sender wobble");
        theft.Detail.Should().Contain("80% → 55%");
    }

    [Fact]
    public void FuelFallingWhileTheGeneratorIsRunning_IsNotTheft()
    {
        // It burned it. That is what generators do.
        SiteSnapshotPayload before = Snapshot(fuel: 80, generatorRunning: true, at: "2026-07-14T12:00:00Z");
        SiteSnapshotPayload now = Snapshot(fuel: 55, generatorRunning: true, at: "2026-07-14T12:15:00Z");

        SnapshotAnomalyDetector.Detect(now, before, Anomalies, Calibration)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.FuelTheft);
    }

    [Fact]
    public void ASmallFuelDip_IsSenderNoise_NotTheft()
    {
        SiteSnapshotPayload before = Snapshot(fuel: 60, generatorRunning: false, at: "2026-07-14T12:00:00Z");
        SiteSnapshotPayload now = Snapshot(fuel: 57, generatorRunning: false, at: "2026-07-14T12:15:00Z");

        SnapshotAnomalyDetector.Detect(now, before, Anomalies, Calibration)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.FuelTheft);
    }

    [Fact]
    public void GeneratorRunningWhileTheGridIsUp_IsOveruse()
    {
        SiteSnapshotPayload now = Snapshot(fuel: 70, generatorRunning: true, gridUp: true);

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Should().Contain(a => a.Kind == EnergyAnomalyKind.GenOveruse);
    }

    [Fact]
    public void GeneratorRunningWhileTheGridIsDown_IsNotOveruse()
    {
        // That is the generator doing its job.
        SiteSnapshotPayload now = Snapshot(fuel: 70, generatorRunning: true, gridUp: false);

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.GenOveruse);
    }

    [Fact]
    public void ABatteryBelowThirtyPercent_IsDegraded()
    {
        // 44 V on a 42–54 V string is 17% — under the 30% bar, but not yet under the 15% one.
        SiteSnapshotPayload now = Snapshot(fuel: 70, generatorRunning: false, batteryVolts: 44.0);

        DetectedEnergyAnomaly degraded = SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Single(a => a.Kind == EnergyAnomalyKind.BatteryDegrade);

        degraded.Severity.Should().Be(EnergyAnomalySeverity.Warn);
    }

    [Fact]
    public void ABatteryBelowFifteenPercent_IsCritical()
    {
        // 43 V is 8% — the site cannot ride out anything at all.
        SiteSnapshotPayload now = Snapshot(fuel: 70, generatorRunning: false, batteryVolts: 43.0);

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Single(a => a.Kind == EnergyAnomalyKind.BatteryDegrade)
            .Severity.Should().Be(EnergyAnomalySeverity.Critical);
    }

    [Fact]
    public void AHealthyBattery_IsNotFlagged()
    {
        SiteSnapshotPayload now = Snapshot(fuel: 70, generatorRunning: false, batteryVolts: 52.0);

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.BatteryDegrade);
    }

    [Fact]
    public void AStaleHeartbeat_MeansTheSiteIsNotReporting()
    {
        SiteSnapshotPayload now = Snapshot(
            fuel: 70, generatorRunning: false,
            at: "2026-07-14T12:15:00Z",
            heartbeat: "2026-07-14T09:00:00Z");

        DetectedEnergyAnomaly offline = SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Single(a => a.Kind == EnergyAnomalyKind.SensorOffline);

        offline.Severity.Should().Be(EnergyAnomalySeverity.Critical, "three hours of silence is not a blip");
    }

    [Fact]
    public void FuelBurningFastEnoughToRunDrySoon_IsAPredictedFault()
    {
        // 10 points burned in 15 minutes = 40 points/hour. At 20% left, that is dry in 30 minutes.
        SiteSnapshotPayload before = Snapshot(fuel: 30, generatorRunning: true, at: "2026-07-14T12:00:00Z");
        SiteSnapshotPayload now = Snapshot(fuel: 20, generatorRunning: true, at: "2026-07-14T12:15:00Z");

        DetectedEnergyAnomaly predicted = SnapshotAnomalyDetector.Detect(now, before, Anomalies, Calibration)
            .Single(a => a.Kind == EnergyAnomalyKind.PredictedFault);

        predicted.Severity.Should().Be(EnergyAnomalySeverity.Critical);
        predicted.Detail.Should().Contain("projected dry");
    }

    [Fact]
    public void AHealthySite_ProducesNoAnomalies()
    {
        SiteSnapshotPayload now = Snapshot(
            fuel: 85, generatorRunning: false, gridUp: true, batteryVolts: 52.0,
            at: "2026-07-14T12:15:00Z", heartbeat: "2026-07-14T12:15:00Z");

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration).Should().BeEmpty();
    }

    [Fact]
    public void ASnapshotWithNoEnvironmentalBlock_ProducesNothing()
    {
        // A RAN-only feed says nothing about the plant. Inferring "sensor offline" from its silence
        // would fire on every one of them.
        var ranOnly = new SiteSnapshotPayload(
            "r1", "MTN", "Production", DateTimeOffset.Parse("2026-07-14T12:15:30Z"),
            new SnapshotSite("S1", "LAG0456", "Site", "Lagos", null, 6.4, 3.4, [], null, null, null, null, null, []),
            Environmental: null, Performance: null, ActiveAlarms: [], Maintenance: null);

        SnapshotAnomalyDetector.Detect(ranOnly, previous: null, Anomalies, Calibration).Should().BeEmpty();
    }

    // ── Tuning ───────────────────────────────────────────────────────────────
    // The point of moving these out of the code. If configuration cannot actually change what the
    // rules do, it is decoration.

    [Fact]
    public void RaisingTheTheftThreshold_SilencesADropThatWouldOtherwiseFire()
    {
        SiteSnapshotPayload before = Snapshot(fuel: 60, generatorRunning: false, at: "2026-07-14T12:00:00Z");
        SiteSnapshotPayload now = Snapshot(fuel: 50, generatorRunning: false, at: "2026-07-14T12:15:00Z");

        // 10 points clears the default 8-point bar.
        SnapshotAnomalyDetector.Detect(now, before, Anomalies, Calibration)
            .Should().Contain(a => a.Kind == EnergyAnomalyKind.FuelTheft);

        // A fleet with noisier senders raises the bar and the same drop goes quiet.
        var tuned = Anomalies with { FuelTheft = Anomalies.FuelTheft with { DropPoints = 15 } };

        SnapshotAnomalyDetector.Detect(now, before, tuned, Calibration)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.FuelTheft);
    }

    [Fact]
    public void DisablingARule_SwitchesItOffEntirely()
    {
        SiteSnapshotPayload now = Snapshot(fuel: 70, generatorRunning: true, gridUp: true);

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Should().Contain(a => a.Kind == EnergyAnomalyKind.GenOveruse);

        var off = Anomalies with
        {
            GeneratorOveruse = Anomalies.GeneratorOveruse with { Enabled = false }
        };

        SnapshotAnomalyDetector.Detect(now, previous: null, off, Calibration)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.GenOveruse);
    }

    [Fact]
    public void RecalibratingTheBatteryWindow_ChangesWhatCountsAsFlat()
    {
        // 44 V is 17% on a 48 V string — flat. On a 24 V string it is off the top of the scale, and
        // reading it as 17% would condemn a perfectly healthy bank. This is why the anomaly rule's
        // percentage thresholds are worthless unless the scale beneath them is configurable too.
        SiteSnapshotPayload now = Snapshot(fuel: 70, generatorRunning: false, batteryVolts: 44.0);

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, Calibration)
            .Should().Contain(a => a.Kind == EnergyAnomalyKind.BatteryDegrade);

        var twentyFourVolt = Calibration with
        {
            Battery = new BatteryCalibration { FloorVolts = 21.0, CeilingVolts = 27.0 }
        };

        SnapshotAnomalyDetector.Detect(now, previous: null, Anomalies, twentyFourVolt)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.BatteryDegrade);
    }

    [Fact]
    public void ShorteningTheRefuellingWindow_NarrowsWhatCountsAsAnImminentDryOut()
    {
        // Burning 4 points/hour with 40% left: dry in 10 hours. Inside the default 12h warning.
        SiteSnapshotPayload before = Snapshot(fuel: 44, generatorRunning: true, at: "2026-07-14T11:15:00Z");
        SiteSnapshotPayload now = Snapshot(fuel: 40, generatorRunning: true, at: "2026-07-14T12:15:00Z");

        SnapshotAnomalyDetector.Detect(now, before, Anomalies, Calibration)
            .Should().Contain(a => a.Kind == EnergyAnomalyKind.PredictedFault);

        // A fleet that can refuel within 6 hours does not need a warning 10 hours out.
        var tuned = Anomalies with
        {
            FuelExhaustion = Anomalies.FuelExhaustion with { WarningHours = 6.0, CriticalHours = 2.0 }
        };

        SnapshotAnomalyDetector.Detect(now, before, tuned, Calibration)
            .Should().NotContain(a => a.Kind == EnergyAnomalyKind.PredictedFault);
    }

    // ── Validation ───────────────────────────────────────────────────────────
    // A misconfigured threshold must stop the boot, not quietly poison the anomalies page.

    [Fact]
    public void AThresholdSetBelowItsWarningLevel_FailsValidation()
    {
        var nonsense = new SnapshotAnomalyOptions
        {
            BatteryDegrade = new BatteryDegradeRuleOptions { WarnBelowPct = 20, CriticalBelowPct = 40 }
        };

        Action validate = nonsense.Validate;

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*CriticalBelowPct*must be below WarnBelowPct*");
    }

    [Fact]
    public void AConfidenceOutsideZeroToOne_FailsValidation()
    {
        var nonsense = new SnapshotAnomalyOptions
        {
            GeneratorOveruse = new GeneratorOveruseRuleOptions { Confidence = 1.4 }
        };

        Action validate = nonsense.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*Confidence*at most 1*");
    }

    [Fact]
    public void AnInvertedBatteryWindow_FailsValidation()
    {
        var nonsense = new SnapshotCalibrationOptions
        {
            Battery = new BatteryCalibration { FloorVolts = 54.0, CeilingVolts = 42.0 }
        };

        Action validate = nonsense.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*CeilingVolts*must be above*");
    }

    [Fact]
    public void TheShippedDefaults_AreThemselvesValid()
    {
        // Guards the defaults against a careless edit — they are what a deployment with no
        // configuration section actually runs on.
        new SnapshotAnomalyOptions().Validate();
        new SnapshotCalibrationOptions().Validate();
    }

    private static SiteSnapshotPayload Snapshot(
        int fuel,
        bool generatorRunning,
        bool gridUp = false,
        double batteryVolts = 50.0,
        string at = "2026-07-14T12:15:00Z",
        string? heartbeat = null) =>
        new(
            RequestId: "r1",
            Provider: "MTN Nigeria",
            Environment: "Production",
            GeneratedAt: DateTimeOffset.Parse(at),
            Site: new SnapshotSite(
                "S1", "LAG0456", "Lekki Tower", "Lagos", null, 6.447, 3.472,
                ["4G"], "Huawei", "Operational", 80, null,
                heartbeat is null ? null : DateTimeOffset.Parse(heartbeat), []),
            Environmental: new SnapshotEnvironmentalMetrics(
                Temperature: 30, Humidity: 60, BatteryVoltage: batteryVolts,
                GeneratorFuelPercent: fuel, GeneratorRunning: generatorRunning,
                MainPowerAvailable: gridUp, AirConditionerStatus: "Running",
                DoorOpen: false, SmokeDetected: false),
            Performance: new SnapshotPerformanceMetrics(
                "15 Minutes", DateTimeOffset.Parse(at), 99.9, 500, 50, 450, 100, 20, 700, 100,
                0.2, 18, 99.5, 0.3, 98.9, 70, []),
            ActiveAlarms: [],
            Maintenance: null);
}
