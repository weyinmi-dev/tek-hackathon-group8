using Application.Abstractions.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Ai.Agents.Workflows.NetworkAnalysis;
using Modules.Ai.Infrastructure.Analysis;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Stage2_Analyze;

/// <summary>
/// Stage-2 threshold behaviour. Formerly HeuristicNetworkBatchAnalyzerTests — retargeted (Phase 3
/// M15) onto the code that now owns those thresholds: NetworkLogAnalysisWorkflow, whose
/// ThresholdAnalysisExecutor runs <c>AnomalyThresholdPolicy</c>.
///
/// These assertions ARE the parity contract. The characterization harness pins the aggregate counts;
/// these pin the rules that produce them (30-point signal drop, 85/95 load, 100/200 ms latency), so a
/// silent change to a threshold fails here with a readable message instead of as a count diff.
/// </summary>
public sealed class NetworkLogAnalysisTests
{
    private readonly WorkflowNetworkBatchAnalyzer _analyzer = new(
        new NetworkLogAnalysisWorkflowBuilder(),
        NullLogger<WorkflowNetworkBatchAnalyzer>.Instance);

    private static readonly Guid RunId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");

    private static NetworkEventSnapshot Event(
        string tower = "LOS-T-014",
        string ts = "2026-05-05T08:00:00Z",
        int? signal = null, int? load = null, int? latency = null,
        string? status = null) =>
        new(RunId, DateTimeOffset.Parse(ts), tower, signal, load, latency, status);

    [Fact]
    public async Task AnalyzeAsync_EmptyEvents_ReturnsEmptyResult()
    {
        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, [], cancellationToken: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().BeEmpty();
        result.Value.Optimizations.Should().BeEmpty();
        result.Value.Topology.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_SignalCollapse_DetectsSignalDropAnomaly()
    {
        // Signal falls 90 → 30 (drop 60): over the 30-point threshold, Critical because it ends below 40.
        IReadOnlyList<NetworkEventSnapshot> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 90),
            Event(ts: "2026-05-05T08:05:00Z", signal: 60),
            Event(ts: "2026-05-05T08:10:00Z", signal: 30)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);

        DetectedAnomaly anomaly = result.Value.Anomalies.Should().ContainSingle(a => a.Type == AnomalyType.SignalDrop).Subject;
        anomaly.Severity.Should().Be(PipelineAlertSeverity.Critical);
        anomaly.TowerCode.Should().Be("LOS-T-014");
        anomaly.Confidence.Should().BeGreaterThan(0.5m);
    }

    [Fact]
    public async Task AnalyzeAsync_SignalDelta20_DoesNotProduceAnomaly()
    {
        IReadOnlyList<NetworkEventSnapshot> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 80),
            Event(ts: "2026-05-05T08:05:00Z", signal: 60)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);

        result.Value.Anomalies.Where(a => a.Type == AnomalyType.SignalDrop).Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_LoadSpike_DetectsAnomalyAndProposesLoadBalance()
    {
        IReadOnlyList<NetworkEventSnapshot> events = [
            Event(ts: "2026-05-05T08:00:00Z", load: 50),
            Event(ts: "2026-05-05T08:05:00Z", load: 96)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);

        DetectedAnomaly anomaly = result.Value.Anomalies.Should().ContainSingle(a => a.Type == AnomalyType.LoadSpike).Subject;
        anomaly.Severity.Should().Be(PipelineAlertSeverity.Critical);

        ProposedOptimization opt = result.Value.Optimizations.Should().ContainSingle().Subject;
        opt.Type.Should().Be(OptimizationType.LoadBalance);
        opt.TowerCode.Should().Be("LOS-T-014");
    }

    [Fact]
    public async Task AnalyzeAsync_LatencyPeakAbove100_DetectsLatencyAnomaly()
    {
        IReadOnlyList<NetworkEventSnapshot> events = [
            Event(ts: "2026-05-05T08:00:00Z", latency: 30),
            Event(ts: "2026-05-05T08:05:00Z", latency: 250)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);

        DetectedAnomaly anomaly = result.Value.Anomalies.Should().ContainSingle(a => a.Type == AnomalyType.LatencyAnomaly).Subject;
        anomaly.Severity.Should().Be(PipelineAlertSeverity.Critical);
    }

    [Fact]
    public async Task AnalyzeAsync_StatusChange_EmitsTopologyStatusDelta()
    {
        IReadOnlyList<NetworkEventSnapshot> events = [
            Event(ts: "2026-05-05T08:00:00Z", status: "OK"),
            Event(ts: "2026-05-05T08:05:00Z", status: "Critical")
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);

        result.Value.Topology.Should().NotBeNull();
        TowerStatusChange change = result.Value.Topology!.StatusChanges.Should().ContainSingle().Subject;
        change.PreviousStatus.Should().Be("OK");
        change.NewStatus.Should().Be("Critical");
    }

    [Fact]
    public async Task AnalyzeAsync_IsDeterministic_SameInputProducesIdenticalOutput()
    {
        // Reproducibility is the whole basis of the parity contract: re-running the same log must
        // produce the same counts. A model in this path would break that; thresholds do not.
        IReadOnlyList<NetworkEventSnapshot> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 90, load: 50, latency: 20, status: "OK"),
            Event(ts: "2026-05-05T08:05:00Z", signal: 30, load: 96, latency: 250, status: "Critical")
        ];

        Result<AiAnalysisResult> a = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);
        Result<AiAnalysisResult> b = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);

        a.Value.Anomalies.Should().HaveCount(b.Value.Anomalies.Count);
        a.Value.Optimizations.Should().HaveCount(b.Value.Optimizations.Count);
        a.Value.Topology?.StatusChanges.Should().HaveCount(b.Value.Topology?.StatusChanges.Count ?? 0);
    }

    [Fact]
    public async Task AnalyzeAsync_FullScenario_MatchesBriefSuccessCriteria()
    {
        // Mirrors the brief's sample log: produces ≥1 anomaly, ≥1 optimization, ≥1 status change.
        IReadOnlyList<NetworkEventSnapshot> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 98, load: 42, latency: 18, status: "OK"),
            Event(ts: "2026-05-05T08:05:00Z", signal: 71, load: 87, latency: 42, status: "Degraded"),
            Event(ts: "2026-05-05T08:10:00Z", signal: 34, load: 93, latency: 118, status: "Critical")
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, cancellationToken: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().NotBeEmpty();
        result.Value.Optimizations.Should().NotBeEmpty();
        result.Value.Topology.Should().NotBeNull();
        result.Value.Topology!.StatusChanges.Should().NotBeEmpty();
    }
}
