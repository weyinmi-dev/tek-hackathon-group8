using Application.Abstractions.Pipeline;
using FluentAssertions;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

/// <summary>
/// Every value in <see cref="SnapshotDerivations"/> is a modelling decision, not a fact the feed
/// gave us. Pinning the endpoints and the clamps here means a change to the curve has to be
/// deliberate — it cannot drift in behind a refactor.
/// </summary>
public sealed class SnapshotDerivationsTests
{
    private static readonly SnapshotCalibrationOptions Calibration = new();
    [Theory]
    [InlineData(-120, 0)]    // cell edge, unusable
    [InlineData(-110, 20)]
    [InlineData(-95, 50)]
    [InlineData(-91, 58)]    // the reference payload
    [InlineData(-80, 80)]
    [InlineData(-70, 100)]   // excellent
    [InlineData(-130, 0)]    // below the floor clamps, never goes negative
    [InlineData(-60, 100)]   // above the ceiling clamps, never exceeds 100
    public void SignalPctFromRsrp_MapsTheRanPlanningWindowLinearly(double rsrpDbm, int expected) =>
        SnapshotDerivations.SignalPctFromRsrp(rsrpDbm, Calibration).Should().Be(expected);

    [Fact]
    public void SignalPctFromRsrp_ReturnsNullWhenTheSnapshotCarriedNoRsrp() =>
        SnapshotDerivations.SignalPctFromRsrp(null, Calibration).Should().BeNull(
            "a missing measurement must stay missing — substituting a number would invent signal data");

    [Theory]
    [InlineData(42.0, 0)]     // low-voltage cutoff
    [InlineData(48.0, 50)]
    [InlineData(48.2, 52)]    // the reference payload
    [InlineData(54.0, 100)]   // float charge
    [InlineData(30.0, 0)]     // clamped
    [InlineData(60.0, 100)]   // clamped
    public void BatteryPctFromVoltage_MapsThe48VStringWindow(double volts, int expected) =>
        SnapshotDerivations.BatteryPctFromVoltage(volts, Calibration).Should().Be(expected);

    [Fact]
    public void TowerStatus_CriticalAlarmOutranksAHealthyScore()
    {
        SnapshotAlarm[] alarms =
        [
            new("ALM-1", "Critical", "Power", "Grid Power Failure", "Active", null, null, null)
        ];

        SnapshotDerivations.TowerStatusFrom(healthScore: 87, alarms, Calibration).Should().Be("CRITICAL");
    }

    [Fact]
    public void TowerStatus_AcknowledgedAlarmStillCounts()
    {
        // Acknowledged means someone has seen it. The fault is still there.
        SnapshotAlarm[] alarms =
        [
            new("ALM-2", "Major", "Cooling", "High Temperature", "Acknowledged", null, null, null)
        ];

        SnapshotDerivations.TowerStatusFrom(healthScore: 95, alarms, Calibration).Should().Be("WARN");
    }

    [Fact]
    public void TowerStatus_ClearedAlarmDoesNotCount()
    {
        SnapshotAlarm[] alarms =
        [
            new("ALM-3", "Critical", "Power", "Grid Power Failure", "Cleared", null, null, null)
        ];

        SnapshotDerivations.TowerStatusFrom(healthScore: 95, alarms, Calibration).Should().Be("OK");
    }

    [Theory]
    [InlineData(95, "OK")]
    [InlineData(80, "OK")]
    [InlineData(79, "WARN")]
    [InlineData(50, "WARN")]
    [InlineData(49, "CRITICAL")]
    public void TowerStatus_FallsBackToTheHealthScoreWhenNoAlarmsAreOpen(int healthScore, string expected) =>
        SnapshotDerivations.TowerStatusFrom(healthScore, [], Calibration).Should().Be(expected);

    [Fact]
    public void ReferencePayloadAlarms_ProduceCritical()
    {
        // Exactly the two alarms in MTN's reference payload: one Critical/Active, one Major/Acknowledged.
        SnapshotAlarm[] alarms =
        [
            new("ALM-100284", "Critical", "Power", "Grid Power Failure", "Active", null, null, null),
            new("ALM-100291", "Major", "Cooling", "High Temperature Warning", "Acknowledged", null, null, null)
        ];

        SnapshotDerivations.TowerStatusFrom(healthScore: 87, alarms, Calibration).Should().Be("CRITICAL");
    }
}
