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
    Task<Result<AiAnalysisResult>> AnalyzeAsync(
        Guid ingestionRunId,
        IReadOnlyList<NetworkEvent> events,
        CancellationToken cancellationToken = default);
}
