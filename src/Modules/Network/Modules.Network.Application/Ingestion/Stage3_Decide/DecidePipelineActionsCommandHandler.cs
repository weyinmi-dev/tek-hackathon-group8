using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

internal sealed class DecidePipelineActionsCommandHandler(
    IIngestionRunRepository runs,
    IDecisionEngine engine,
    IAlertSnapshotProvider alerts,
    ITowerSnapshotProvider towers,
    ILogger<DecidePipelineActionsCommandHandler> logger)
    : ICommandHandler<DecidePipelineActionsCommand, IReadOnlyList<PipelineAction>>
{
    public async Task<Result<IReadOnlyList<PipelineAction>>> Handle(
        DecidePipelineActionsCommand request,
        CancellationToken cancellationToken)
    {
        IngestionRun? run = await runs.GetByIdAsync(request.IngestionRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure<IReadOnlyList<PipelineAction>>(Error.NotFound(
                "Network.Ingestion.RunNotFound",
                $"Ingestion run {request.IngestionRunId} not found."));
        }

        if (run.Status != IngestionStatus.Deciding)
        {
            return Result.Failure<IReadOnlyList<PipelineAction>>(Error.Conflict(
                "Network.Ingestion.WrongStage",
                $"Run {run.Id} is in {run.Status}, not Deciding — orchestrator must transition first."));
        }

        IReadOnlyList<AlertSnapshot> activeAlerts = await alerts.GetActiveAsync(cancellationToken);
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers = await towers.GetCurrentAsync(cancellationToken);

        IReadOnlyList<PipelineAction> actions = engine.Decide(request.Analysis, activeAlerts, currentTowers);

        logger.LogInformation(
            "Decision engine produced {ActionCount} actions for run {IngestionRunId} " +
            "({AlertCount} active alerts, {TowerCount} towers known)",
            actions.Count, run.Id, activeAlerts.Count, currentTowers.Count);

        return Result.Success(actions);
    }
}
