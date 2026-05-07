using Application.Abstractions.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
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

        // ── Alert actions ────────────────────────────────────────────────────
        List<AlertActionRequest> alertRequests = request.Actions
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

        // ── Tower actions ────────────────────────────────────────────────────
        int towerUpdates = 0;
        foreach (UpdateTowerAction action in request.Actions.OfType<UpdateTowerAction>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Tower? tower = await towers.GetByCodeAsync(action.TowerCode, cancellationToken);
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
            TowerUpdates: towerUpdates);

        logger.LogInformation(
            "Run {IngestionRunId}: applied {AlertsCreated} new alerts, {AlertsUpdated} alert recurrences, {TowerUpdates} tower updates",
            run.Id, counts.AlertsCreated, counts.AlertsUpdated, counts.TowerUpdates);

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
