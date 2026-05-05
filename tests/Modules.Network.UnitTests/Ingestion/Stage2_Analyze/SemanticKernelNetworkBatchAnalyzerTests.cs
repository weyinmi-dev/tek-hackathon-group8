using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Ai.Infrastructure.Pipeline;
using Modules.Ai.Infrastructure.Pipeline.Skills;
using Modules.Ai.Infrastructure.Pipeline.Validators;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Stage2_Analyze;

/// <summary>
/// Tests the wrapper logic — composition, schema validation, retry-once on transient
/// failures. The actual SK kernel is never instantiated; that's exercised only by
/// live integration tests in PR 6.
/// </summary>
public sealed class SemanticKernelNetworkBatchAnalyzerTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static NetworkEvent SampleEvent() => NetworkEvent.Create(
        RunId,
        DateTimeOffset.Parse("2026-05-05T08:00:00Z"),
        "LOS-T-014",
        signalPct: 50, loadPct: 80, latencyMs: 30,
        rawStatus: "Degraded", rawPayload: null);

    private static DetectedAnomaly ValidAnomaly() => new(
        TowerCode: "LOS-T-014",
        Type: AnomalyType.SignalDrop,
        Severity: PipelineAlertSeverity.Warn,
        Confidence: 0.8m,
        DetectedAt: DateTimeOffset.Parse("2026-05-05T08:00:00Z"),
        Explanation: "evidence",
        Metrics: new Dictionary<string, decimal>());

    [Fact]
    public async Task AnalyzeAsync_EmptyEvents_ShortCircuitsToEmptyResult()
    {
        var anomalySkill = new StubAnomalySkill(_ => throw new InvalidOperationException("should not be called"));
        var optimizationSkill = new StubOptimizationSkill(_ => throw new InvalidOperationException("should not be called"));
        var topologySkill = new StubTopologySkill(_ => throw new InvalidOperationException("should not be called"));

        SemanticKernelNetworkBatchAnalyzer analyzer = NewAnalyzer(anomalySkill, optimizationSkill, topologySkill);

        Result<AiAnalysisResult> result = await analyzer.AnalyzeAsync(RunId, [], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().BeEmpty();
        result.Value.Optimizations.Should().BeEmpty();
        result.Value.Topology.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_AllSkillsSucceed_ReturnsCombinedResult()
    {
        var anomalySkill = new StubAnomalySkill(_ => Result.Success<IReadOnlyList<DetectedAnomaly>>([ValidAnomaly()]));
        var optimizationSkill = new StubOptimizationSkill(_ => Result.Success<IReadOnlyList<ProposedOptimization>>([]));
        var topologySkill = new StubTopologySkill(_ => Result.Success<TopologyDelta?>(null));

        SemanticKernelNetworkBatchAnalyzer analyzer = NewAnalyzer(anomalySkill, optimizationSkill, topologySkill);

        Result<AiAnalysisResult> result = await analyzer.AnalyzeAsync(RunId, [SampleEvent()], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Anomalies.Should().ContainSingle();
    }

    [Fact]
    public async Task AnalyzeAsync_SkillReturnsMalformedJson_RetriesOnce()
    {
        int attempts = 0;
        var anomalySkill = new StubAnomalySkill(_ =>
        {
            attempts++;
            return attempts == 1
                ? Result.Failure<IReadOnlyList<DetectedAnomaly>>(Error.Failure("Network.Ingestion.AiMalformedJson", "boom"))
                : Result.Success<IReadOnlyList<DetectedAnomaly>>([ValidAnomaly()]);
        });
        var optimizationSkill = new StubOptimizationSkill(_ => Result.Success<IReadOnlyList<ProposedOptimization>>([]));
        var topologySkill = new StubTopologySkill(_ => Result.Success<TopologyDelta?>(null));

        SemanticKernelNetworkBatchAnalyzer analyzer = NewAnalyzer(anomalySkill, optimizationSkill, topologySkill);

        Result<AiAnalysisResult> result = await analyzer.AnalyzeAsync(RunId, [SampleEvent()], CancellationToken.None);

        attempts.Should().Be(2);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_SkillFailsTwice_PropagatesError()
    {
        var anomalySkill = new StubAnomalySkill(_ =>
            Result.Failure<IReadOnlyList<DetectedAnomaly>>(Error.Failure("Network.Ingestion.AiMalformedJson", "boom")));
        var optimizationSkill = new StubOptimizationSkill(_ => Result.Success<IReadOnlyList<ProposedOptimization>>([]));
        var topologySkill = new StubTopologySkill(_ => Result.Success<TopologyDelta?>(null));

        SemanticKernelNetworkBatchAnalyzer analyzer = NewAnalyzer(anomalySkill, optimizationSkill, topologySkill);

        Result<AiAnalysisResult> result = await analyzer.AnalyzeAsync(RunId, [SampleEvent()], CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.AiMalformedJson");
    }

    [Fact]
    public async Task AnalyzeAsync_NonRetryableError_DoesNotRetry()
    {
        int attempts = 0;
        var anomalySkill = new StubAnomalySkill(_ =>
        {
            attempts++;
            return Result.Failure<IReadOnlyList<DetectedAnomaly>>(
                Error.Failure("Network.Ingestion.AiInvocationFailed", "auth"));
        });
        var optimizationSkill = new StubOptimizationSkill(_ => Result.Success<IReadOnlyList<ProposedOptimization>>([]));
        var topologySkill = new StubTopologySkill(_ => Result.Success<TopologyDelta?>(null));

        SemanticKernelNetworkBatchAnalyzer analyzer = NewAnalyzer(anomalySkill, optimizationSkill, topologySkill);

        Result<AiAnalysisResult> result = await analyzer.AnalyzeAsync(RunId, [SampleEvent()], CancellationToken.None);

        attempts.Should().Be(1);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_AnomalyFailsSchemaValidationOnCombinedResult_PropagatesError()
    {
        // Anomaly with confidence > 1 — slips past per-call validation (the per-call skill stub
        // doesn't validate), but the combined-result validator catches it.
        var bad = ValidAnomaly() with { Confidence = 1.5m };

        var anomalySkill = new StubAnomalySkill(_ => Result.Success<IReadOnlyList<DetectedAnomaly>>([bad]));
        var optimizationSkill = new StubOptimizationSkill(_ => Result.Success<IReadOnlyList<ProposedOptimization>>([]));
        var topologySkill = new StubTopologySkill(_ => Result.Success<TopologyDelta?>(null));

        SemanticKernelNetworkBatchAnalyzer analyzer = NewAnalyzer(anomalySkill, optimizationSkill, topologySkill);

        Result<AiAnalysisResult> result = await analyzer.AnalyzeAsync(RunId, [SampleEvent()], CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.AiSchemaInvalid");
    }

    private static SemanticKernelNetworkBatchAnalyzer NewAnalyzer(
        INetworkAnomalySkill anomalySkill,
        INetworkOptimizationSkill optimizationSkill,
        INetworkTopologyMappingSkill topologySkill) =>
        new(
            anomalySkill,
            optimizationSkill,
            topologySkill,
            new AiAnalysisResultValidator(),
            NullLogger<SemanticKernelNetworkBatchAnalyzer>.Instance);

    private sealed class StubAnomalySkill(Func<int, Result<IReadOnlyList<DetectedAnomaly>>> respond) : INetworkAnomalySkill
    {
        private int _calls;
        public Task<Result<IReadOnlyList<DetectedAnomaly>>> InvokeAsync(string _, CancellationToken __ = default) =>
            Task.FromResult(respond(++_calls));
    }

    private sealed class StubOptimizationSkill(Func<int, Result<IReadOnlyList<ProposedOptimization>>> respond) : INetworkOptimizationSkill
    {
        private int _calls;
        public Task<Result<IReadOnlyList<ProposedOptimization>>> InvokeAsync(string _, CancellationToken __ = default) =>
            Task.FromResult(respond(++_calls));
    }

    private sealed class StubTopologySkill(Func<int, Result<TopologyDelta?>> respond) : INetworkTopologyMappingSkill
    {
        private int _calls;
        public Task<Result<TopologyDelta?>> InvokeAsync(string _, CancellationToken __ = default) =>
            Task.FromResult(respond(++_calls));
    }
}
