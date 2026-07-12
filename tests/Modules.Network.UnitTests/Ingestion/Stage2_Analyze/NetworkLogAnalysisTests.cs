using FluentAssertions;
using Modules.Ai.Infrastructure.Pipeline;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Stage2_Analyze;

public sealed class HeuristicNetworkBatchAnalyzerTests
{
    private readonly HeuristicNetworkBatchAnalyzer _analyzer = new();
    private static readonly Guid RunId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");

    private static NetworkEvent Event(
        string tower = "LOS-T-014",
        string ts = "2026-05-05T08:00:00Z",
        int? signal = null, int? load = null, int? latency = null,
        string? status = null) =>
        NetworkEvent.Create(RunId, DateTimeOffset.Parse(ts), tower, signal, load, latency, status, null);

    [Fact]
    public async Task AnalyzeAsync_EmptyEvents_ReturnsEmptyResult()
    {
        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, [], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().BeEmpty();
        result.Value.Optimizations.Should().BeEmpty();
        result.Value.Topology.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_SignalCollapse_DetectsSignalDropAnomaly()
    {
        // Signal falls 90 → 30 (drop 60); over the 30-pp threshold, severity Critical (lastSignal < 40)
        IReadOnlyList<NetworkEvent> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 90),
            Event(ts: "2026-05-05T08:05:00Z", signal: 60),
            Event(ts: "2026-05-05T08:10:00Z", signal: 30)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);

        DetectedAnomaly anomaly = result.Value.Anomalies.Should().ContainSingle(a => a.Type == AnomalyType.SignalDrop).Subject;
        anomaly.Severity.Should().Be(PipelineAlertSeverity.Critical);
        anomaly.TowerCode.Should().Be("LOS-T-014");
        anomaly.Confidence.Should().BeGreaterThan(0.5m);
    }

    [Fact]
    public async Task AnalyzeAsync_SignalDelta20_DoesNotProduceAnomaly()
    {
        IReadOnlyList<NetworkEvent> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 80),
            Event(ts: "2026-05-05T08:05:00Z", signal: 60)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);

        result.Value.Anomalies.Where(a => a.Type == AnomalyType.SignalDrop).Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_LoadSpike_DetectsAnomalyAndProposesLoadBalance()
    {
        IReadOnlyList<NetworkEvent> events = [
            Event(ts: "2026-05-05T08:00:00Z", load: 50),
            Event(ts: "2026-05-05T08:05:00Z", load: 96)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);

        DetectedAnomaly anomaly = result.Value.Anomalies.Should().ContainSingle(a => a.Type == AnomalyType.LoadSpike).Subject;
        anomaly.Severity.Should().Be(PipelineAlertSeverity.Critical);

        ProposedOptimization opt = result.Value.Optimizations.Should().ContainSingle().Subject;
        opt.Type.Should().Be(OptimizationType.LoadBalance);
        opt.TowerCode.Should().Be("LOS-T-014");
    }

    [Fact]
    public async Task AnalyzeAsync_LatencyPeakAbove100_DetectsLatencyAnomaly()
    {
        IReadOnlyList<NetworkEvent> events = [
            Event(ts: "2026-05-05T08:00:00Z", latency: 30),
            Event(ts: "2026-05-05T08:05:00Z", latency: 250)
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);

        DetectedAnomaly anomaly = result.Value.Anomalies.Should().ContainSingle(a => a.Type == AnomalyType.LatencyAnomaly).Subject;
        anomaly.Severity.Should().Be(PipelineAlertSeverity.Critical);
    }

    [Fact]
    public async Task AnalyzeAsync_StatusChange_EmitsTopologyStatusDelta()
    {
        IReadOnlyList<NetworkEvent> events = [
            Event(ts: "2026-05-05T08:00:00Z", status: "OK"),
            Event(ts: "2026-05-05T08:05:00Z", status: "Critical")
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);

        result.Value.Topology.Should().NotBeNull();
        TowerStatusChange change = result.Value.Topology!.StatusChanges.Should().ContainSingle().Subject;
        change.PreviousStatus.Should().Be("OK");
        change.NewStatus.Should().Be("Critical");
    }

    [Fact]
    public async Task AnalyzeAsync_IsDeterministic_SameInputProducesIdenticalOutput()
    {
        // Critical for integration tests: re-running the analyzer must give the same result
        // every time, otherwise the success criteria are flaky.
        IReadOnlyList<NetworkEvent> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 90, load: 50, latency: 20, status: "OK"),
            Event(ts: "2026-05-05T08:05:00Z", signal: 30, load: 96, latency: 250, status: "Critical")
        ];

        Result<AiAnalysisResult> a = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);
        Result<AiAnalysisResult> b = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);

        a.Value.Anomalies.Should().HaveCount(b.Value.Anomalies.Count);
        a.Value.Optimizations.Should().HaveCount(b.Value.Optimizations.Count);
        a.Value.Topology?.StatusChanges.Should().HaveCount(b.Value.Topology?.StatusChanges.Count ?? 0);
    }

    [Fact]
    public async Task AnalyzeAsync_FullScenario_MatchesBriefSuccessCriteria()
    {
        // Mirrors the brief's sample log: produces ≥1 anomaly, ≥1 optimization, ≥1 status change.
        IReadOnlyList<NetworkEvent> events = [
            Event(ts: "2026-05-05T08:00:00Z", signal: 98, load: 42, latency: 18, status: "OK"),
            Event(ts: "2026-05-05T08:05:00Z", signal: 71, load: 87, latency: 42, status: "Degraded"),
            Event(ts: "2026-05-05T08:10:00Z", signal: 34, load: 93, latency: 118, status: "Critical")
        ];

        Result<AiAnalysisResult> result = await _analyzer.AnalyzeAsync(RunId, events, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().NotBeEmpty();
        result.Value.Optimizations.Should().NotBeEmpty();
        result.Value.Topology.Should().NotBeNull();
        result.Value.Topology!.StatusChanges.Should().NotBeEmpty();
    }
}
