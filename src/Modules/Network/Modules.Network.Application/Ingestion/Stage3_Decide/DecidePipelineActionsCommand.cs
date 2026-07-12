using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Application.Abstractions.Pipeline;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Stage 3 — translates schema-validated AI output into a list of <see cref="PipelineAction"/>s.
/// Returns the list; does NOT execute any of them. Stage 4 (PR 5) is the only stage allowed
/// to mutate persistent state.
///
/// Pre-condition: the IngestionRun must be in <c>Deciding</c>; the orchestrator owns transitions.
/// </summary>
public sealed record DecidePipelineActionsCommand(
    Guid IngestionRunId,
    AiAnalysisResult Analysis) : ICommand<IReadOnlyList<PipelineAction>>, IIngestionPipelineRequest
{
    public string StageName => "Decide";
}
