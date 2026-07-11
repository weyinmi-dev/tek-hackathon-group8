using SharedKernel;

namespace Application.Abstractions.Pipeline;

/// <summary>
/// Stage-2 of the network-log pipeline: turn parsed events into anomalies, optimizations and a
/// topology delta. Implemented on the AI side — from M12 by <c>NetworkLogAnalysisWorkflow</c>,
/// previously by the Semantic Kernel / heuristic batch analyzers.
/// </summary>
/// <remarks>
/// The contract lives in the shared layer rather than inside Network: the Network module consumes it
/// and the Ai module implements it, so neither has to reference the other. Events arrive as neutral
/// <see cref="NetworkEventSnapshot"/> records instead of Network domain entities, which is what lets
/// the analyzer sit outside Network entirely.
/// </remarks>
public interface INetworkBatchAnalyzer
{
    /// <param name="mcpFilePath">
    /// Telcopilot-relative path of the staged source file (e.g. <c>uploads/a1b2c3d4/events.csv</c>).
    /// When provided, an implementation may read the raw file for additional prompt context.
    /// Null-safe — deterministic implementations ignore it.
    /// </param>
    Task<Result<AiAnalysisResult>> AnalyzeAsync(
        Guid ingestionRunId,
        IReadOnlyList<NetworkEventSnapshot> events,
        string? mcpFilePath = null,
        CancellationToken cancellationToken = default);
}
