using Application.Abstractions.Pipeline;
using FluentAssertions;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

/// <summary>
/// The snapshot feed goes through the same JSON parser as the flat logs, routed on the shape of
/// the document. These tests pin both halves of that contract: the snapshot decodes completely,
/// and the flat-log forms that shipped before it still parse exactly as they did.
/// </summary>
public sealed class SiteSnapshotParserTests
{
    private readonly JsonNetworkLogParser _parser = new(new SnapshotCalibrationOptions());

    private async Task<NetworkLogParseResult> ParseAsync(string json)
    {
        Result<NetworkLogParseResult> result = await _parser.ParseAsync(
            ParserTestHelpers.SampleRunId, ParserTestHelpers.Utf8Stream(json));

        result.IsSuccess.Should().BeTrue(
            "the reference payload must parse; error was: {0}",
            result.IsFailure ? result.Error.Description : "none");

        return result.Value;
    }

    [Fact]
    public async Task ReferencePayload_DecodesIntoOneSnapshotAndOneReading()
    {
        NetworkLogParseResult result = await ParseAsync(SiteSnapshotFixture.MtnLagos);

        result.Snapshots.Should().HaveCount(1);
        result.Events.Should().HaveCount(1, "a snapshot is a single point-in-time reading of one site");
    }

    [Fact]
    public async Task ReferencePayload_CarriesProvenance()
    {
        SiteSnapshotPayload snapshot = (await ParseAsync(SiteSnapshotFixture.MtnLagos)).Snapshots[0];

        snapshot.RequestId.Should().Be("6a0f8b94-4a1d-43a4-a9c8-f0e87691d551");
        snapshot.Provider.Should().Be("MTN Nigeria");
        snapshot.Environment.Should().Be("Production");
        snapshot.GeneratedAt.Should().Be(DateTimeOffset.Parse("2026-07-14T12:15:30Z"));
    }

    [Fact]
    public async Task ReferencePayload_DecodesSiteIdentity()
    {
        SnapshotSite site = (await ParseAsync(SiteSnapshotFixture.MtnLagos)).Snapshots[0].Site;

        site.SiteId.Should().Be("MTN-LAG-0456");
        site.SiteCode.Should().Be("LAG0456", "the site code is the join key to Tower.Code and Energy Site.Code");
        site.SiteName.Should().Be("Lekki Phase 1 Tower");
        site.Region.Should().Be("Lagos");
        site.Cluster.Should().Be("Lagos East");
        site.Vendor.Should().Be("Huawei");
        site.HealthScore.Should().Be(87);
        site.Latitude.Should().BeApproximately(6.447325, 1e-6);
        site.Longitude.Should().BeApproximately(3.472181, 1e-6);
        site.Technology.Should().Equal("2G", "3G", "4G", "5G");
        site.CommissionedDate.Should().Be(new DateOnly(2021, 8, 16));
        site.LastHeartbeat.Should().Be(DateTimeOffset.Parse("2026-07-14T12:15:01Z"));
    }

    [Fact]
    public async Task ReferencePayload_DecodesAllFourEquipmentItems_IncludingOneWithNoModel()
    {
        SnapshotSite site = (await ParseAsync(SiteSnapshotFixture.MtnLagos)).Snapshots[0].Site;

        site.Equipment.Should().HaveCount(4);
        site.Equipment.Select(e => e.EquipmentId)
            .Should().Equal("BBU-001", "RRU-001", "GEN-001", "BAT-001");

        // The battery bank has no "model" property at all — an absent optional must survive as
        // null rather than failing the parse or defaulting to empty.
        SnapshotEquipment battery = site.Equipment.Single(e => e.EquipmentId == "BAT-001");
        battery.Model.Should().BeNull();
        battery.Status.Should().Be("Charging");
    }

    [Fact]
    public async Task ReferencePayload_DecodesEnvironmentalMetrics()
    {
        SnapshotEnvironmentalMetrics env = (await ParseAsync(SiteSnapshotFixture.MtnLagos)).Snapshots[0].Environmental!;

        env.Temperature.Should().Be(37.6);
        env.BatteryVoltage.Should().Be(48.2);
        env.GeneratorFuelPercent.Should().Be(41);
        env.GeneratorRunning.Should().BeTrue();
        env.MainPowerAvailable.Should().BeFalse("the grid is down in the reference payload");
        env.SmokeDetected.Should().BeFalse();
    }

    [Fact]
    public async Task ReferencePayload_DecodesPerformanceMetricsAndKpis()
    {
        SnapshotPerformanceMetrics perf = (await ParseAsync(SiteSnapshotFixture.MtnLagos)).Snapshots[0].Performance!;

        perf.CapturedAt.Should().Be(DateTimeOffset.Parse("2026-07-14T12:15:00Z"));
        perf.LatencyMs.Should().Be(18);
        perf.CellUtilizationPercent.Should().Be(76);
        perf.ConnectedUsers.Should().Be(682);
        perf.DownlinkTrafficGb.Should().Be(148.3);

        perf.Kpis.Should().HaveCount(3);
        SnapshotDerivations.Kpi(perf.Kpis, "RSRP").Should().Be(-91);
        SnapshotDerivations.Kpi(perf.Kpis, "SINR").Should().Be(25.7);
        SnapshotDerivations.Kpi(perf.Kpis, "prb utilization")
            .Should().Be(71.2, "KPI lookup is case-insensitive — vendor casing varies");
    }

    [Fact]
    public async Task ReferencePayload_DecodesAlarmsAndMaintenance()
    {
        SiteSnapshotPayload snapshot = (await ParseAsync(SiteSnapshotFixture.MtnLagos)).Snapshots[0];

        snapshot.ActiveAlarms.Should().HaveCount(2);
        SnapshotAlarm critical = snapshot.ActiveAlarms.Single(a => a.AlarmId == "ALM-100284");
        critical.Severity.Should().Be("Critical");
        critical.Category.Should().Be("Power");
        critical.RaisedAt.Should().Be(DateTimeOffset.Parse("2026-07-14T11:47:00Z"));

        SnapshotTicket ticket = snapshot.Maintenance!.OpenTickets.Single();
        ticket.TicketId.Should().Be("TT-20491");
        ticket.AssignedEngineer!.EngineerId.Should().Be("ENG-091");
        ticket.AssignedEngineer.Name.Should().Be("Adewale Johnson");

        snapshot.Maintenance.MaintenanceHistory.Should().HaveCount(2);
        snapshot.Maintenance.LastMaintenanceDate.Should().Be(new DateOnly(2026, 6, 22));
        snapshot.Maintenance.NextScheduledMaintenance.Should().Be(new DateOnly(2026, 8, 20));
    }

    /// <summary>
    /// The flattened reading is what Stages 2–4 actually score. If this projection is wrong the
    /// snapshot silently stops producing alerts, so every field is pinned.
    /// </summary>
    [Fact]
    public async Task ReferencePayload_FlattensToAReadingTheAnalyzerCanScore()
    {
        NetworkEvent reading = (await ParseAsync(SiteSnapshotFixture.MtnLagos)).Events[0];

        reading.TowerCode.Should().Be("LAG0456");
        reading.OccurredAt.Should().Be(
            DateTimeOffset.Parse("2026-07-14T12:15:00Z"),
            "the reading is stamped when the measurements were captured, not when the document was generated");

        reading.SignalPct.Should().Be(58, "RSRP of -91 dBm maps to 58% on the -120..-70 dBm scale");
        reading.LoadPct.Should().Be(76, "cell utilization is the load");
        reading.LatencyMs.Should().Be(18);

        reading.RawStatus.Should().Be(
            "CRITICAL",
            "an open Critical alarm outranks the site's own flattering health score of 87");

        reading.RawPayload.Should().NotBeNull("the full document is retained so nothing is lost by flattening");
        reading.RawPayload.Should().Contain("ALM-100284");
    }

    /// <summary>Vendor conclusions must not survive the parse — see SiteSnapshotPayload's rationale.</summary>
    [Fact]
    public async Task VendorSuppliedAiHints_AreIgnored()
    {
        const string withHints = """
        {
          "requestId": "r1", "provider": "MTN Nigeria", "environment": "Production",
          "generatedAt": "2026-07-14T12:15:30Z",
          "site": { "siteId": "S1", "siteCode": "LAG0456", "siteName": "N", "region": "Lagos" },
          "aiHints": {
            "predictedFailureRisk": "Medium",
            "predictedFailureProbability": 0.63,
            "recommendedAction": "Inspect power subsystem within 24 hours.",
            "estimatedTimeToFailureHours": 72
          }
        }
        """;

        NetworkLogParseResult result = await ParseAsync(withHints);

        // The unknown property must not fail the parse...
        result.Snapshots.Should().HaveCount(1);

        // ...and must not be re-serialised into the stored canonical document, or a later reader
        // could resurrect it and treat a vendor's verdict as our own.
        result.Snapshots[0].Serialize().Should().NotContain("aiHints");
        result.Snapshots[0].Serialize().Should().NotContain("recommendedAction");
    }

    [Fact]
    public async Task BatchedSnapshots_DecodeIntoOneReadingEach()
    {
        const string batch = """
        {
          "snapshots": [
            {
              "requestId": "r1", "provider": "MTN Nigeria", "environment": "Production",
              "generatedAt": "2026-07-14T12:15:30Z",
              "site": { "siteId": "S1", "siteCode": "LAG0456", "siteName": "A", "region": "Lagos" }
            },
            {
              "requestId": "r2", "provider": "MTN Nigeria", "environment": "Production",
              "generatedAt": "2026-07-14T12:16:30Z",
              "site": { "siteId": "S2", "siteCode": "ABJ0102", "siteName": "B", "region": "Abuja" }
            }
          ]
        }
        """;

        NetworkLogParseResult result = await ParseAsync(batch);

        result.Snapshots.Should().HaveCount(2);
        result.Events.Select(e => e.TowerCode).Should().Equal("LAG0456", "ABJ0102");
    }

    /// <summary>
    /// A bare top-level array of snapshots — the shape MTN's multi-site NOC export actually uses.
    /// It was originally routed to the flat-log parser, which reported "missing required column
    /// 'timestamp'" because a snapshot has no such column. It is told apart from a flat log by the
    /// nested `site` object on its first element.
    /// </summary>
    [Fact]
    public async Task TopLevelArrayOfSnapshots_DecodesAsSnapshots()
    {
        const string array = """
        [
          {
            "requestId": "r1", "provider": "MTN Nigeria", "environment": "Production",
            "generatedAt": "2026-07-14T12:15:30Z",
            "site": { "siteId": "S1", "siteCode": "LAG0102", "siteName": "VI Tower", "region": "Lagos" },
            "activeAlarms": []
          },
          {
            "requestId": "r2", "provider": "MTN Nigeria", "environment": "Production",
            "generatedAt": "2026-07-14T12:16:30Z",
            "site": { "siteId": "S2", "siteCode": "LAG0312", "siteName": "Ikeja Tower", "region": "Lagos" },
            "activeAlarms": [
              { "alarmId": "ALM-300182", "severity": "Critical", "category": "Power", "status": "Active" }
            ]
          }
        ]
        """;

        NetworkLogParseResult result = await ParseAsync(array);

        result.Snapshots.Should().HaveCount(2);
        result.Snapshots.Select(s => s.Site.SiteCode).Should().Equal("LAG0102", "LAG0312");
        result.Events.Select(e => e.TowerCode).Should().Equal("LAG0102", "LAG0312");

        result.Events[0].RawStatus.Should().Be("OK", "the first site reports no alarms");
        result.Events[1].RawStatus.Should().Be("CRITICAL", "the second site has an open Critical alarm");
    }

    [Fact]
    public async Task SiteCode_IsNormalisedToUpperInvariant()
    {
        const string lowercase = """
        {
          "requestId": "r1", "provider": "MTN", "environment": "Production",
          "generatedAt": "2026-07-14T12:15:30Z",
          "site": { "siteId": "S1", "siteCode": "  lag0456  ", "siteName": "A", "region": "Lagos" }
        }
        """;

        NetworkLogParseResult result = await ParseAsync(lowercase);

        result.Snapshots[0].Site.SiteCode.Should().Be("LAG0456");
        result.Events[0].TowerCode.Should().Be("LAG0456");
    }

    [Fact]
    public async Task SnapshotWithoutSiteCode_FailsTheStage()
    {
        const string noCode = """
        {
          "requestId": "r1", "provider": "MTN", "environment": "Production",
          "generatedAt": "2026-07-14T12:15:30Z",
          "site": { "siteId": "S1", "siteName": "A", "region": "Lagos" }
        }
        """;

        Result<NetworkLogParseResult> result = await _parser.ParseAsync(
            ParserTestHelpers.SampleRunId, ParserTestHelpers.Utf8Stream(noCode));

        result.IsFailure.Should().BeTrue("without a site code the snapshot cannot be joined to anything");
        result.Error.Description.Should().Contain("siteCode");
    }

    /// <summary>
    /// Regression guard for the routing decision. The snapshot shape was added to the existing JSON
    /// parser rather than to a second registry entry; if the shape sniff ever over-matches, flat
    /// logs would break silently. These two forms are the ones that shipped before snapshots.
    /// </summary>
    [Fact]
    public async Task FlatEventArray_StillParsesAsBefore_AndProducesNoSnapshot()
    {
        const string flat = """
        [
          { "timestamp": "2026-07-14T12:00:00Z", "tower_code": "TWR-001", "signal_pct": 80, "load_pct": 40, "latency_ms": 30 }
        ]
        """;

        NetworkLogParseResult result = await ParseAsync(flat);

        result.Events.Should().HaveCount(1);
        result.Events[0].TowerCode.Should().Be("TWR-001");
        result.Events[0].SignalPct.Should().Be(80);
        result.Snapshots.Should().BeEmpty();
    }

    [Fact]
    public async Task EventsEnvelope_StillParsesAsBefore_AndProducesNoSnapshot()
    {
        const string envelope = """
        {
          "events": [
            { "timestamp": "2026-07-14T12:00:00Z", "tower_code": "TWR-002", "signal_pct": 55 }
          ]
        }
        """;

        NetworkLogParseResult result = await ParseAsync(envelope);

        result.Events.Should().HaveCount(1);
        result.Events[0].TowerCode.Should().Be("TWR-002");
        result.Snapshots.Should().BeEmpty();
    }
}
