using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Application.Abstractions.Pipeline;
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

        // The analyzer contract is module-neutral (Phase 3 M12): project the domain entities into
        // snapshots so the analysis side never sees Network's domain model.
        List<NetworkEventSnapshot> snapshots = events
            .Select(e => new NetworkEventSnapshot(
                e.IngestionRunId,
                e.OccurredAt,
                e.TowerCode,
                e.SignalPct,
                e.LoadPct,
                e.LatencyMs,
                e.RawStatus))
            .ToList();

        Result<AiAnalysisResult> analysisResult = await analyzer.AnalyzeAsync(
            run.Id, snapshots, request.McpFilePath, cancellationToken);
        return analysisResult;
    }
}
