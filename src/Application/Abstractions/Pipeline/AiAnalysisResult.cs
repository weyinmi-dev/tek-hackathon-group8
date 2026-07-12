namespace Application.Abstractions.Pipeline;

/// <summary>
/// The composite result of one Stage-2 analysis: what the analyzer found in a batch of parsed
/// network events. Produced by <c>NetworkLogAnalysisWorkflow</c> via <c>INetworkBatchAnalyzer</c>,
/// and consumed by the decision stage. Because the shape is strongly typed rather than free-form
/// model output, the decision layer cannot be handed something it does not understand.
/// </summary>
public sealed record AiAnalysisResult(
    IReadOnlyList<DetectedAnomaly> Anomalies,
    IReadOnlyList<ProposedOptimization> Optimizations,
    TopologyDelta? Topology,
    string? EnergyObservationsJson = null)
{
    public static readonly AiAnalysisResult Empty = new(
        Array.Empty<DetectedAnomaly>(),
        Array.Empty<ProposedOptimization>(),
        Topology: null,
        EnergyObservationsJson: null);
}

