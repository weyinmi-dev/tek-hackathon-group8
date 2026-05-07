using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage2_Analyze;

/// <summary>
/// Cross-module abstraction implemented by the AI module. Network depends on this
/// interface; Modules.Ai.Infrastructure provides a SemanticKernel-backed implementation
/// that returns schema-validated <see cref="AiAnalysisResult"/>. Coupling is one-way:
/// AI knows about Network contracts, Network does not know about Semantic Kernel.
/// </summary>
public interface INetworkBatchAnalyzer
{
    /// <param name="mcpFilePath">
    /// Telcopilot-relative path of the staged source file (e.g.
    /// <c>uploads/a1b2c3d4/events.csv</c>). When provided, the SK-backed
    /// implementation reads the raw file and includes it as additional context in
    /// prompts. Null-safe — heuristic and stub implementations ignore it.
    /// </param>
    Task<Result<AiAnalysisResult>> AnalyzeAsync(
        Guid ingestionRunId,
        IReadOnlyList<NetworkEvent> events,
        string? mcpFilePath = null,
        CancellationToken cancellationToken = default);
}
