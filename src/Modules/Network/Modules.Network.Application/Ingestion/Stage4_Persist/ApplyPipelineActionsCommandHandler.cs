using Application.Abstractions.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Domain;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Towers;
using SharedKernel;
using DomainOptimizationType = Modules.Network.Domain.Optimizations.OptimizationType;

namespace Modules.Network.Application.Ingestion.Stage4_Persist;

internal sealed class ApplyPipelineActionsCommandHandler(
    IIngestionRunRepository runs,
    ITowerSnapshotProvider towerSnapshots,
    ITowerRepository towers,
    IAlertActionExecutor alertExecutor,
    IEnergySyncExecutor energyExecutor,
    SnapshotSyncApplier snapshotApplier,
    ISender sender,
    IUnitOfWork unitOfWork,
    ILogger<ApplyPipelineActionsCommandHandler> logger)
    : ICommandHandler<ApplyPipelineActionsCommand, PipelineActionCounts>
{
    public async Task<Result<PipelineActionCounts>> Handle(
        ApplyPipelineActionsCommand request,
        CancellationToken cancellationToken)
    {
        IngestionRun? run = await runs.GetByIdAsync(request.IngestionRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure<PipelineActionCounts>(Error.NotFound(
                "Network.Ingestion.RunNotFound",
                $"Ingestion run {request.IngestionRunId} not found."));
        }

        if (run.Status != IngestionStatus.Persisting)
        {
            return Result.Failure<PipelineActionCounts>(Error.Conflict(
                "Network.Ingestion.WrongStage",
                $"Run {run.Id} is in {run.Status}, not Persisting — orchestrator must transition first."));
        }

        IReadOnlyDictionary<string, TowerSnapshot> snapshot =
            await towerSnapshots.GetCurrentAsync(cancellationToken);

        // ── Snapshot synchronisation (Network-owned aggregates) ──────────────
        // Runs first: a snapshot may bring a tower into existence, and the alert and energy work
        // below is keyed on that tower's code. These stage into NetworkDbContext and are committed
        // by the SaveChanges at the end of this handler; the Alerts and Energy executors commit
        // their own contexts, since each module owns its own unit of work.
        SnapshotSyncCounts sync = await snapshotApplier.ApplyAsync(request.Actions, cancellationToken);

        // ── Alert actions ────────────────────────────────────────────────────
        // Both the AI-detected anomalies and the OSS-reported alarms converge here, on the same
        // executor and the same fingerprint-based create-vs-update path.
        var alertRequests = request.Actions
            .Select(action => TryBuildAlertRequest(action, snapshot))
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        AlertActionsResult alertResult = new(0, 0);
        if (alertRequests.Count > 0)
        {
            Result<AlertActionsResult> dispatched = await alertExecutor.ExecuteAsync(alertRequests, cancellationToken);
            if (dispatched.IsFailure)
            {
                return Result.Failure<PipelineActionCounts>(dispatched.Error);
            }
            alertResult = dispatched.Value;
        }

        // ── Alarm clearance ──────────────────────────────────────────────────
        int alertsResolved = 0;
        var resolutions = request.Actions
            .OfType<ResolveAlarmAction>()
            .Select(a => new AlertResolutionRequest(a.AnomalyFingerprint, a.Reason))
            .ToList();

        AlertResolutionsResult resolutionResult = new(0);
        if (resolutions.Count > 0)
        {
            Result<AlertResolutionsResult> resolved = await alertExecutor.ResolveAsync(resolutions, cancellationToken);
            if (resolved.IsFailure)
            {
                return Result.Failure<PipelineActionCounts>(resolved.Error);
            }
            resolutionResult = resolved.Value;
            alertsResolved = resolutionResult.AlertsResolved;
        }

        // ── Energy synchronisation ───────────────────────────────────────────
        EnergySyncResult energyResult = new(0, 0, 0);
        var energyRequests = request.Actions
            .OfType<SyncEnergySiteAction>()
            .Select(a => new EnergySyncRequest(
                a.SiteCode, a.Name, a.Region, a.BatteryPct, a.DieselPct, a.GridUp,
                a.SourceWire, a.HasOpenAlarm, a.AnomalyNote, a.ObservedAtUtc, a.Anomalies))
            .ToList();

        if (energyRequests.Count > 0)
        {
            Result<EnergySyncResult> dispatched = await energyExecutor.ExecuteAsync(energyRequests, cancellationToken);
            if (dispatched.IsFailure)
            {
                return Result.Failure<PipelineActionCounts>(dispatched.Error);
            }
            energyResult = dispatched.Value;
        }

        // ── Tower actions (AI path) ──────────────────────────────────────────
        // The analyzer's topology delta. A snapshot upload flattens into a reading that runs through
        // the analyzer too, so this can land on the SAME tower the snapshot just upserted — which is
        // why these changes are recorded rather than merely counted, and why the totals de-duplicate
        // by entity. One tower touched twice is one record changed.
        int towerUpdates = 0;
        var aiTowerChanges = new List<SyncChange>();

        foreach (UpdateTowerAction action in request.Actions.OfType<UpdateTowerAction>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Tracked load: this tower is about to be mutated and committed by the unit of work below.
            Tower? tower = await towers.GetForUpdateAsync(action.TowerCode, cancellationToken);
            if (tower is null)
            {
                // Decision engine already filters unknown towers, but defend the invariant.
                logger.LogWarning(
                    "Tower update skipped — code {TowerCode} not found in current set",
                    action.TowerCode);
                continue;
            }

            ApplyTowerAction(tower, action);
            towerUpdates++;

            aiTowerChanges.Add(new SyncChange(
                EntityType: "Tower",
                EntityKey: tower.Code,
                Action: SyncAction.Updated,
                SiteCode: tower.Code,
                Detail: $"{tower.Status} - signal {tower.SignalPct}% - load {tower.LoadPct}%" +
                        (tower.Issue is null ? string.Empty : $" - {tower.Issue}")));
        }

        // ── Optimizations: dispatch CreateOptimizationCommand per action ────
        // Each dispatch goes through the standard MediatR pipeline (logging + validation
        // behaviors) and stages the entity in the same NetworkDbContext; the SaveChanges
        // below commits all of them with the tower updates in one transaction.
        int optimizationsCreated = 0;
        DateTimeOffset proposedAt = DateTimeOffset.UtcNow;
        foreach (CreateOptimizationAction action in request.Actions.OfType<CreateOptimizationAction>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var command = new CreateOptimizationCommand(
                IngestionRunId: run.Id,
                TowerCode: action.Source.TowerCode,
                AnomalyFingerprint: action.AnomalyFingerprint,
                Type: MapOptimizationType(action.Source.Type),
                EstimatedImpact: action.Source.EstimatedImpact,
                Rationale: action.Source.Rationale,
                ProposedAt: proposedAt);

            Result<Guid> dispatched = await sender.Send(command, cancellationToken);
            if (dispatched.IsFailure)
            {
                return Result.Failure<PipelineActionCounts>(dispatched.Error);
            }
            optimizationsCreated++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var counts = new PipelineActionCounts(
            AlertsCreated: alertResult.AlertsCreated,
            AlertsUpdated: alertResult.AlertsUpdated,
            OptimizationsCreated: optimizationsCreated,

            // Per-aggregate detail. These may double-count a tower both paths touched — the headline
            // totals de-duplicate by entity, so they don't.
            TowerUpdates: towerUpdates + sync.TowerUpdates,

            TowersCreated: sync.TowersCreated,
            AlertsResolved: alertsResolved,
            SitesCreated: energyResult.SitesCreated,
            SitesUpdated: energyResult.SitesUpdated,
            TelemetryRowsAppended: energyResult.TelemetryRowsAppended,
            EquipmentCreated: sync.EquipmentCreated,
            EquipmentUpdated: sync.EquipmentUpdated,
            EquipmentRetired: sync.EquipmentRetired,
            TicketsCreated: sync.TicketsCreated,
            TicketsUpdated: sync.TicketsUpdated,
            TicketsCompleted: sync.TicketsCompleted,
            TicketsArchived: sync.TicketsArchived,
            EngineersCreated: sync.EngineersCreated,
            EngineersUpdated: sync.EngineersUpdated,
            AnomaliesCreated: energyResult.AnomaliesCreated,
            AnomaliesUpdated: energyResult.AnomaliesUpdated,
            AnomaliesResolved: energyResult.AnomaliesResolved,
            Warnings: sync.Warnings,

            // The itemised change list, assembled from every module that touched something. Ordered
            // created → updated → archived so the report reads the way an operator scans it.
            Changes: [
                .. sync.Changes,
                .. aiTowerChanges,
                .. alertResult.Changes,
                .. resolutionResult.Changes,
                .. energyResult.Changes
            ]);

        logger.LogInformation(
            "Run {IngestionRunId}: {Created} created, {Updated} updated, {Archived} archived " +
            "({AlertsCreated} new alerts, {AlertsResolved} alerts resolved, {TowersCreated} towers created)",
            run.Id, counts.TotalCreated, counts.TotalUpdated, counts.TotalArchived,
            counts.AlertsCreated, counts.AlertsResolved, counts.TowersCreated);

        return Result.Success(counts);
    }

    private static AlertActionRequest? TryBuildAlertRequest(
        PipelineAction action,
        IReadOnlyDictionary<string, TowerSnapshot> towers)
    {
        return action switch
        {
            CreateAlertAction create => Build(create.Source, create.AnomalyFingerprint, existingId: null, towers),
            UpdateAlertAction update => Build(update.Source, update.AnomalyFingerprint, update.ExistingAlertId, towers),

            // An OSS alarm arrives already fully described — the provider told us the severity, the
            // site, and the cause — so unlike an inferred anomaly there is nothing to look up or
            // interpret. It carries full confidence because it is a reported fact, not a detection.
            SyncAlarmAction alarm => new AlertActionRequest(
                AnomalyFingerprint: alarm.AnomalyFingerprint,
                ExistingAlertId: alarm.ExistingAlertId,
                SeverityWire: alarm.SeverityWire,
                TowerCode: alarm.TowerCode,
                Region: alarm.Region,
                Title: alarm.Title,
                AiCause: alarm.Cause,
                Confidence: 1.0,
                SubscribersAffected: 0,
                DetectedAtUtc: alarm.RaisedAtUtc),

            _ => null
        };
    }

    private static AlertActionRequest? Build(
        DetectedAnomaly anomaly,
        string fingerprint,
        Guid? existingId,
        IReadOnlyDictionary<string, TowerSnapshot> towers)
    {
        towers.TryGetValue(anomaly.TowerCode, out TowerSnapshot? snapshot);
        return new AlertActionRequest(
            AnomalyFingerprint: fingerprint,
            ExistingAlertId: existingId,
            SeverityWire: anomaly.Severity.ToString().ToUpperInvariant(),
            TowerCode: anomaly.TowerCode,
            Region: snapshot?.Region ?? "Unknown",
            Title: BuildTitle(anomaly),
            AiCause: anomaly.Explanation,
            Confidence: (double)anomaly.Confidence,
            SubscribersAffected: 0,
            DetectedAtUtc: anomaly.DetectedAt.UtcDateTime);
    }

    private static string BuildTitle(DetectedAnomaly anomaly) =>
        $"{FriendlyType(anomaly.Type)} on {anomaly.TowerCode}";

    private static string FriendlyType(AnomalyType type) => type switch
    {
        AnomalyType.SignalDrop => "Signal drop",
        AnomalyType.LoadSpike => "Load spike",
        AnomalyType.OutagePattern => "Outage pattern",
        AnomalyType.LatencyAnomaly => "Latency anomaly",
        AnomalyType.PacketLoss => "Packet loss",
        AnomalyType.PowerInstability => "Power instability",
        _ => type.ToString()
    };

    private static void ApplyTowerAction(Tower tower, UpdateTowerAction action)
    {
        // Fall back to current values when the AI didn't produce a new metric for that
        // dimension; the existing UpdateMetrics signature requires non-null values for all.
        int signalPct = action.MetricUpdate?.SignalPct ?? tower.SignalPct;
        int loadPct = action.MetricUpdate?.LoadPct ?? tower.LoadPct;
        TowerStatus newStatus = action.StatusChange is { } sc
            ? ParseStatus(sc.NewStatus)
            : tower.Status;
        string? issue = action.StatusChange?.Reason ?? tower.Issue;

        tower.UpdateMetrics(signalPct, loadPct, newStatus, issue);
    }

    private static TowerStatus ParseStatus(string wire) => wire?.ToUpperInvariant() switch
    {
        "CRITICAL" => TowerStatus.Critical,
        "WARN" or "WARNING" or "DEGRADED" => TowerStatus.Warn,
        _ => TowerStatus.Ok
    };

    /// <summary>
    /// Map the wire-level OptimizationType (used for AI output) to the Domain enum.
    /// They share members today but live in different namespaces so Domain stays free
    /// of Application dependencies.
    /// </summary>
    private static DomainOptimizationType MapOptimizationType(OptimizationType wire) => wire switch
    {
        OptimizationType.LoadBalance => DomainOptimizationType.LoadBalance,
        OptimizationType.PowerAdjust => DomainOptimizationType.PowerAdjust,
        OptimizationType.RouteReconfigure => DomainOptimizationType.RouteReconfigure,
        OptimizationType.AntennaRetune => DomainOptimizationType.AntennaRetune,
        OptimizationType.CapacityExpansion => DomainOptimizationType.CapacityExpansion,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown optimization type")
    };
}
