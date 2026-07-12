using FluentAssertions;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Xunit;
using static Modules.Network.UnitTests.Ingestion.Decisions.DecisionFixtures;

namespace Modules.Network.UnitTests.Ingestion.Decisions;

public sealed class DefaultDecisionEngineTests
{
    private readonly DefaultDecisionEngine _engine = new(new DecisionEngineOptions());

    // ── Empty / null input ─────────────────────────────────────────────────────

    [Fact]
    public void Decide_EmptyAi_ReturnsNoActions()
    {
        IReadOnlyList<PipelineAction> actions = _engine.Decide(AiResult(), [], Towers());

        actions.Should().BeEmpty();
    }

    [Fact]
    public void Decide_NullAi_Throws()
    {
        Action act = () => _engine.Decide(null!, [], Towers());

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Anomaly → Alert decisions ─────────────────────────────────────────────

    [Fact]
    public void Decide_AnomalyBelowConfidenceThreshold_IsDropped()
    {
        DetectedAnomaly weak = Anomaly(confidence: 0.49m);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [weak]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().BeEmpty();
    }

    [Fact]
    public void Decide_AnomalyAtConfidenceThreshold_IsKept()
    {
        DetectedAnomaly atThreshold = Anomaly(confidence: 0.5m);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [atThreshold]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().ContainSingle().Which.Should().BeOfType<CreateAlertAction>();
    }

    [Fact]
    public void Decide_AnomalyWithoutMatchingAlert_EmitsCreateAction()
    {
        DetectedAnomaly anomaly = Anomaly();

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [anomaly]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().HaveCount(1);
        var create = actions[0].Should().BeOfType<CreateAlertAction>().Subject;
        create.Source.Should().Be(anomaly);
        create.AnomalyFingerprint.Should().Be(FingerprintFor(anomaly));
    }

    [Fact]
    public void Decide_AnomalyMatchingActiveAlert_EmitsUpdateActionWithExistingId()
    {
        DetectedAnomaly anomaly = Anomaly();
        string fingerprint = FingerprintFor(anomaly);
        Guid existingId = Guid.NewGuid();

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [anomaly]),
            activeAlerts: [ActiveAlert(fingerprint, id: existingId)],
            currentTowers: Towers());

        actions.Should().HaveCount(1);
        var update = actions[0].Should().BeOfType<UpdateAlertAction>().Subject;
        update.ExistingAlertId.Should().Be(existingId);
        update.AnomalyFingerprint.Should().Be(fingerprint);
    }

    [Fact]
    public void Decide_AnomalyMatchingResolvedAlert_StillCreatesNewAlert()
    {
        DetectedAnomaly anomaly = Anomaly();
        string fingerprint = FingerprintFor(anomaly);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [anomaly]),
            activeAlerts: [ActiveAlert(fingerprint, resolved: true)],
            currentTowers: Towers());

        actions.Should().ContainSingle().Which.Should().BeOfType<CreateAlertAction>();
    }

    // ── Within-batch dedup ────────────────────────────────────────────────────

    [Fact]
    public void Decide_DuplicateAnomaliesInSameBatch_CollapseToSingleAction()
    {
        DetectedAnomaly a = Anomaly(severity: PipelineAlertSeverity.Warn, confidence: 0.7m);
        DetectedAnomaly b = Anomaly(severity: PipelineAlertSeverity.Warn, confidence: 0.7m);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [a, b]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().ContainSingle();
    }

    [Fact]
    public void Decide_DuplicateAnomaliesWithDifferentSeverity_KeepsMostSevere()
    {
        DetectedAnomaly warn = Anomaly(severity: PipelineAlertSeverity.Warn, confidence: 0.95m);
        DetectedAnomaly critical = Anomaly(severity: PipelineAlertSeverity.Critical, confidence: 0.6m);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [warn, critical]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().ContainSingle();
        var create = actions[0].Should().BeOfType<CreateAlertAction>().Subject;
        create.Source.Severity.Should().Be(PipelineAlertSeverity.Critical);
    }

    [Fact]
    public void Decide_DuplicateAnomaliesSameSeverity_KeepsHigherConfidence()
    {
        DetectedAnomaly low = Anomaly(severity: PipelineAlertSeverity.Warn, confidence: 0.6m);
        DetectedAnomaly high = Anomaly(severity: PipelineAlertSeverity.Warn, confidence: 0.92m);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [low, high]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().ContainSingle();
        var create = actions[0].Should().BeOfType<CreateAlertAction>().Subject;
        create.Source.Confidence.Should().Be(0.92m);
    }

    [Fact]
    public void Decide_AnomaliesForDifferentTowers_AreSeparateActions()
    {
        DetectedAnomaly a = Anomaly(tower: "LOS-T-014");
        DetectedAnomaly b = Anomaly(tower: "ABV-T-007");

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [a, b]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().HaveCount(2);
        actions.Should().AllBeOfType<CreateAlertAction>();
    }

    // ── Optimization decisions ────────────────────────────────────────────────

    [Fact]
    public void Decide_OptimizationBelowImpactThreshold_IsDropped()
    {
        ProposedOptimization weak = Optimization(impact: 0.19m);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(optimizations: [weak]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().BeEmpty();
    }

    [Fact]
    public void Decide_OptimizationAboveImpact_EmitsCreateAction()
    {
        ProposedOptimization strong = Optimization(impact: 0.6m);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(optimizations: [strong]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().ContainSingle().Which.Should().BeOfType<CreateOptimizationAction>();
    }

    [Fact]
    public void Decide_OptimizationCorrelatesWithAnomalyOnSameTower()
    {
        DetectedAnomaly anomaly = Anomaly(tower: "LOS-T-014");
        ProposedOptimization optimization = Optimization(tower: "LOS-T-014");

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [anomaly], optimizations: [optimization]),
            activeAlerts: [],
            currentTowers: Towers());

        var optAction = actions.OfType<CreateOptimizationAction>().Should().ContainSingle().Subject;
        optAction.AnomalyFingerprint.Should().Be(FingerprintFor(anomaly));
    }

    [Fact]
    public void Decide_OptimizationWithoutMatchingAnomaly_HasEmptyFingerprint()
    {
        ProposedOptimization orphan = Optimization(tower: "ABV-T-007");

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(optimizations: [orphan]),
            activeAlerts: [],
            currentTowers: Towers());

        var optAction = actions.OfType<CreateOptimizationAction>().Should().ContainSingle().Subject;
        optAction.AnomalyFingerprint.Should().BeEmpty();
    }

    // ── Topology decisions ────────────────────────────────────────────────────

    [Fact]
    public void Decide_StatusChangeForUnknownTower_IsSkipped()
    {
        var topology = new TopologyDelta(
            StatusChanges: [new TowerStatusChange("UNKNOWN-T-999", "ok", "critical", null)],
            MetricUpdates: []);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014")));

        actions.Should().BeEmpty();
    }

    [Fact]
    public void Decide_StatusChangeMatchingCurrent_IsSkipped()
    {
        var topology = new TopologyDelta(
            StatusChanges: [new TowerStatusChange("LOS-T-014", "ok", "ok", null)],
            MetricUpdates: []);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014", status: "ok")));

        actions.Should().BeEmpty();
    }

    [Fact]
    public void Decide_StatusChangeFromOkToCritical_EmitsUpdateTowerAction()
    {
        var change = new TowerStatusChange("LOS-T-014", "ok", "critical", "outage");
        var topology = new TopologyDelta(StatusChanges: [change], MetricUpdates: []);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014", status: "ok")));

        var update = actions.Should().ContainSingle().Subject.Should().BeOfType<UpdateTowerAction>().Subject;
        update.TowerCode.Should().Be("LOS-T-014");
        update.StatusChange.Should().Be(change);
        update.MetricUpdate.Should().BeNull();
    }

    [Fact]
    public void Decide_MetricUpdateWithinThreshold_IsSkipped()
    {
        // current load is 50; new is 53; default threshold is 5 — not material.
        var metric = new TowerMetricUpdate("LOS-T-014", SignalPct: null, LoadPct: 53, LatencyMs: null);
        var topology = new TopologyDelta(StatusChanges: [], MetricUpdates: [metric]);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014", loadPct: 50)));

        actions.Should().BeEmpty();
    }

    [Fact]
    public void Decide_MetricUpdateAboveThreshold_EmitsUpdateAction()
    {
        // signal jumps from 90 to 60 (delta 30, > 5)
        var metric = new TowerMetricUpdate("LOS-T-014", SignalPct: 60, LoadPct: null, LatencyMs: null);
        var topology = new TopologyDelta(StatusChanges: [], MetricUpdates: [metric]);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014", signalPct: 90)));

        var update = actions.Should().ContainSingle().Subject.Should().BeOfType<UpdateTowerAction>().Subject;
        update.MetricUpdate.Should().Be(metric);
        update.StatusChange.Should().BeNull();
    }

    [Fact]
    public void Decide_LatencyAboveAbsoluteThreshold_EmitsAction()
    {
        // default LatencyDeltaMsThreshold is 10
        var metric = new TowerMetricUpdate("LOS-T-014", SignalPct: null, LoadPct: null, LatencyMs: 50);
        var topology = new TopologyDelta(StatusChanges: [], MetricUpdates: [metric]);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014")));

        actions.Should().ContainSingle().Which.Should().BeOfType<UpdateTowerAction>();
    }

    [Fact]
    public void Decide_StatusChangeAndMetricUpdateForSameTower_AreMerged()
    {
        var change = new TowerStatusChange("LOS-T-014", "ok", "warn", null);
        var metric = new TowerMetricUpdate("LOS-T-014", SignalPct: 50, LoadPct: null, LatencyMs: null);

        var topology = new TopologyDelta(
            StatusChanges: [change],
            MetricUpdates: [metric]);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014", status: "ok", signalPct: 90)));

        var update = actions.Should().ContainSingle().Subject.Should().BeOfType<UpdateTowerAction>().Subject;
        update.StatusChange.Should().Be(change);
        update.MetricUpdate.Should().Be(metric);
    }

    [Fact]
    public void Decide_TowerCodeMatchingIsCaseInsensitive()
    {
        var change = new TowerStatusChange("los-t-014", "ok", "critical", null);
        var topology = new TopologyDelta(StatusChanges: [change], MetricUpdates: []);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014", status: "ok")));

        actions.Should().ContainSingle().Which.Should().BeOfType<UpdateTowerAction>();
    }

    // ── End-to-end happy path ─────────────────────────────────────────────────

    [Fact]
    public void Decide_FullScenario_ProducesAlertOptimizationAndTowerActions()
    {
        DetectedAnomaly anomaly = Anomaly(tower: "LOS-T-014", severity: PipelineAlertSeverity.Critical, confidence: 0.92m);
        ProposedOptimization optimization = Optimization(tower: "LOS-T-014", impact: 0.7m);
        var change = new TowerStatusChange("LOS-T-014", "ok", "critical", "signal collapse");
        var topology = new TopologyDelta(StatusChanges: [change], MetricUpdates: []);

        IReadOnlyList<PipelineAction> actions = _engine.Decide(
            AiResult(anomalies: [anomaly], optimizations: [optimization], topology: topology),
            activeAlerts: [],
            currentTowers: Towers(Tower("LOS-T-014", status: "ok")));

        actions.Should().HaveCount(3);
        actions.OfType<CreateAlertAction>().Should().ContainSingle();
        actions.OfType<CreateOptimizationAction>().Should().ContainSingle()
            .Which.AnomalyFingerprint.Should().Be(FingerprintFor(anomaly));
        actions.OfType<UpdateTowerAction>().Should().ContainSingle();
    }

    [Fact]
    public void Decide_CustomConfidenceThreshold_ChangesFiltering()
    {
        var stricter = new DefaultDecisionEngine(new DecisionEngineOptions { MinAnomalyConfidence = 0.9m });
        DetectedAnomaly mid = Anomaly(confidence: 0.8m);

        IReadOnlyList<PipelineAction> actions = stricter.Decide(
            AiResult(anomalies: [mid]),
            activeAlerts: [],
            currentTowers: Towers());

        actions.Should().BeEmpty();
    }
}
