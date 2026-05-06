using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage2_Analyze;

internal sealed class AnalyzeNetworkBatchCommandHandler(
    IIngestionRunRepository runs,
    INetworkBatchAnalyzer analyzer,
    ILogger<AnalyzeNetworkBatchCommandHandler> logger)
    : ICommandHandler<AnalyzeNetworkBatchCommand, AiAnalysisResult>
{
    public async Task<Result<AiAnalysisResult>> Handle(
        AnalyzeNetworkBatchCommand request,
        CancellationToken cancellationToken)
    {
        IngestionRun? run = await runs.GetByIdAsync(request.IngestionRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure<AiAnalysisResult>(Error.NotFound(
                "Network.Ingestion.RunNotFound",
                $"Ingestion run {request.IngestionRunId} not found."));
        }

        if (run.Status != IngestionStatus.Analyzing)
        {
            return Result.Failure<AiAnalysisResult>(Error.Conflict(
                "Network.Ingestion.WrongStage",
                $"Run {run.Id} is in {run.Status}, not Analyzing — orchestrator must transition first."));
        }

        IReadOnlyList<NetworkEvent> events = await runs.ListEventsAsync(run.Id, cancellationToken);
        logger.LogInformation(
            "Run {IngestionRunId}: invoking AI analyzer over {EventCount} events",
            run.Id, events.Count);

        Result<AiAnalysisResult> analysisResult = await analyzer.AnalyzeAsync(
            run.Id, events, request.McpFilePath, cancellationToken);
        return analysisResult;
    }
}
