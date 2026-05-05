using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

namespace Modules.Network.Application.Ingestion.Stage2_Analyze;

/// <summary>
/// Stage 2 — sends the parsed events through the AI analyzer and returns a
/// schema-validated <see cref="AiAnalysisResult"/>. Pre-condition: the run must be in
/// <c>Analyzing</c>; the orchestrator owns transitions.
/// </summary>
public sealed record AnalyzeNetworkBatchCommand(
    Guid IngestionRunId) : ICommand<AiAnalysisResult>, IIngestionPipelineRequest
{
    public string StageName => "Analyze";
}
