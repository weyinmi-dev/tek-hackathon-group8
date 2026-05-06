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
    Guid IngestionRunId,
    /// <summary>
    /// Telcopilot-relative path of the staged source file (e.g.
    /// <c>uploads/a1b2c3d4/events.csv</c>). Forwarded from the orchestrator so the
    /// AI analyzer can read the raw file content for enriched prompt context via
    /// <c>IFileStagingService.TryReadTextAsync</c>. Null when not available.
    /// </summary>
    string? McpFilePath = null) : ICommand<AiAnalysisResult>, IIngestionPipelineRequest
{
    public string StageName => "Analyze";
}
