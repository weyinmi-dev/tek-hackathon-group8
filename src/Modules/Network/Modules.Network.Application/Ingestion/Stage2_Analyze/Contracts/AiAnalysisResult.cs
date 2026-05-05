namespace Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

/// <summary>
/// The composite, schema-enforced result of one Stage 2 AI invocation. The
/// SemanticKernel-backed implementation of <c>INetworkBatchAnalyzer</c> populates this
/// from strongly-typed <c>[KernelFunction]</c> returns, and FluentValidation rejects
/// any result that violates the contract before it reaches the decision layer.
/// </summary>
public sealed record AiAnalysisResult(
    IReadOnlyList<DetectedAnomaly> Anomalies,
    IReadOnlyList<ProposedOptimization> Optimizations,
    TopologyDelta? Topology)
{
    public static readonly AiAnalysisResult Empty = new(
        Array.Empty<DetectedAnomaly>(),
        Array.Empty<ProposedOptimization>(),
        Topology: null);
}
