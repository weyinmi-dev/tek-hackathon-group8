using Application.Abstractions.Pipeline;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Modules.Ai.Agents.Workflows.NetworkAnalysis;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Analysis;

/// <summary>
/// Stage-2 analysis backed by <c>NetworkLogAnalysisWorkflow</c> (Phase 3 M12) — the MAF replacement
/// for <c>SemanticKernelNetworkBatchAnalyzer</c> and <c>HeuristicNetworkBatchAnalyzer</c>, both of
/// which are deleted. The workflow's threshold executor owns the outcome, so the pipeline's
/// anomaly/optimization/topology counts are identical to the pre-migration baseline and the analysis
/// no longer needs a model to notice an obvious breach.
/// </summary>
/// <remarks>
/// No checkpoint manager: Stage-2 runs inside the ingestion request and is retried by the pipeline
/// orchestrator, so durability here would buy nothing (durability is a hosting concern — Phase 2 D6,
/// applied where it matters, in the async document pipeline).
/// </remarks>
internal sealed class WorkflowNetworkBatchAnalyzer(
    NetworkLogAnalysisWorkflowBuilder builder,
    ILogger<WorkflowNetworkBatchAnalyzer> logger) : INetworkBatchAnalyzer
{
    public async Task<Result<AiAnalysisResult>> AnalyzeAsync(
        Guid ingestionRunId,
        IReadOnlyList<NetworkEventSnapshot> events,
        string? mcpFilePath = null,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Result.Success(AiAnalysisResult.Empty);
        }

        Workflow workflow = builder.Build();
        var request = new AnalyzeNetworkBatchRequest(ingestionRunId, events, mcpFilePath);

        StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, request, cancellationToken: cancellationToken);

        AiAnalysisResult? result = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            if (evt is WorkflowOutputEvent { Data: AiAnalysisResult analysis })
            {
                result = analysis;
            }
        }

        if (result is null)
        {
            logger.LogWarning("Run {IngestionRunId}: the analysis workflow produced no result.", ingestionRunId);
            return Result.Success(AiAnalysisResult.Empty);
        }

        logger.LogInformation(
            "Run {IngestionRunId}: analysis produced {AnomalyCount} anomaly(ies), {OptimizationCount} optimization(s).",
            ingestionRunId, result.Anomalies.Count, result.Optimizations.Count);

        return Result.Success(result);
    }
}
