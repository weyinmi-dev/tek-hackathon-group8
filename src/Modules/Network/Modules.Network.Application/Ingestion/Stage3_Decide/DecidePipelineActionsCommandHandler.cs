using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

internal sealed class DecidePipelineActionsCommandHandler(
    IIngestionRunRepository runs,
    IDecisionEngine engine,
    ISiteSnapshotPlanner snapshotPlanner,
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

        // What the analyzer inferred from the readings.
        var actions = new List<PipelineAction>(
            engine.Decide(request.Analysis, activeAlerts, currentTowers));

        int aiActions = actions.Count;

        // What the provider reported outright, if this upload carried snapshots. Read back from the
        // run rather than passed in memory, exactly as Stage 2 reads its events back — a re-run of
        // this stage replays the same stored document and plans the same actions.
        IReadOnlyList<SiteSnapshotRecord> snapshots = await runs.ListSnapshotsAsync(run.Id, cancellationToken);

        if (snapshots.Count > 0)
        {
            List<SiteSnapshotPayload> payloads = [];
            foreach (SiteSnapshotRecord record in snapshots)
            {
                SiteSnapshotPayload? payload = SiteSnapshotPayload.Deserialize(record.RawJson);
                if (payload is null)
                {
                    // Stage 1 wrote this row from a payload it had already validated, so failing to
                    // read it back means the stored document is corrupt — not merely unexpected.
                    return Result.Failure<IReadOnlyList<PipelineAction>>(Error.Failure(
                        "Network.Ingestion.CorruptSnapshot",
                        $"Snapshot {record.Id} for site {record.SiteCode} could not be deserialised."));
                }

                payloads.Add(payload);
            }

            // The reading immediately before each of these, per site. The anomaly rules that matter
            // are about change — fuel that fell while the generator was off is theft, and a single
            // reading cannot show that. Loaded here and passed in so the planner stays pure.
            var previousBySite = new Dictionary<string, SiteSnapshotPayload>(StringComparer.OrdinalIgnoreCase);
            foreach (SiteSnapshotRecord record in snapshots)
            {
                SiteSnapshotRecord? prior = await runs.GetPreviousSnapshotForSiteAsync(
                    record.SiteCode, record.CapturedAt ?? record.GeneratedAt, cancellationToken);

                if (prior is null)
                {
                    continue;
                }

                SiteSnapshotPayload? priorPayload = SiteSnapshotPayload.Deserialize(prior.RawJson);
                if (priorPayload is not null)
                {
                    previousBySite[record.SiteCode] = priorPayload;
                }
            }

            actions.AddRange(snapshotPlanner.Plan(payloads, activeAlerts, currentTowers, previousBySite));
        }

        logger.LogInformation(
            "Stage 3 produced {ActionCount} actions for run {IngestionRunId} — {AiActions} from analysis, " +
            "{SyncActions} from {SnapshotCount} site snapshot(s) ({AlertCount} active alerts, {TowerCount} towers known)",
            actions.Count, run.Id, aiActions, actions.Count - aiActions, snapshots.Count,
            activeAlerts.Count, currentTowers.Count);

        return Result.Success<IReadOnlyList<PipelineAction>>(actions);
    }
}
