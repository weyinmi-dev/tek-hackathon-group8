using Application.Abstractions.Events;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using Modules.Network.Domain;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Pipeline;

internal sealed class ProcessNetworkLogCommandHandler(
    IIngestionRunRepository runs,
    IUnitOfWork unitOfWork,
    ISender sender,
    IEventBus eventBus,
    IFileStagingService staging,
    ILogger<ProcessNetworkLogCommandHandler> logger)
    : ICommandHandler<ProcessNetworkLogCommand, IngestionRunSummary>
{
    public async Task<Result<IngestionRunSummary>> Handle(
        ProcessNetworkLogCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Buffer the file once so we can hash it AND re-read it inside Stage 1's parser.
        byte[] bytes = await ReadAllBytesAsync(request.Content, cancellationToken);
        string contentHash = Fingerprints.ContentHash(bytes);

        // Persist to the telcopilot uploads directory so the MCP filesystem server
        // (FS plugin) can expose it for copilot queries and Stage-2 can read the raw
        // content for enriched SK prompts. Failure is non-fatal — the pipeline runs
        // with events-only context if staging is unavailable.
        string? mcpFilePath = request.McpFilePath
            ?? await staging.StageAsync(contentHash, request.FileName, bytes, cancellationToken);

        // 2. File-level idempotency. Re-uploading the same bytes resolves to the original run,
        //    but only when that run succeeded. Failed runs are re-processed so a retry after a
        //    fix (e.g. a transient AI error) actually runs the pipeline instead of replaying the
        //    stored failure.
        IngestionRun? prior;
        try
        {
            prior = await runs.GetByContentHashAsync(contentHash, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to query existing run for content hash {ContentHash}", contentHash);
            return Result.Failure<IngestionRunSummary>(
                Error.Failure("Network.Ingestion.DbLookupFailed", ex.Message));
        }

        if (prior is not null && prior.Status == IngestionStatus.Completed)
        {
            logger.LogInformation(
                "Ingestion short-circuited — content hash {ContentHash} already processed as run {IngestionRunId}",
                contentHash, prior.Id);

            IReadOnlyList<SiteSnapshotRecord> priorSnapshots =
                await runs.ListSnapshotsAsync(prior.Id, cancellationToken);

            return Result.Success(BuildSummary(prior, deduplicatedFromPriorRun: true, priorSnapshots));
        }

        // 3. Create the run. SaveChanges immediately so subsequent stage handlers can find it by ID.
        IngestionRun run;
        try
        {
            run = IngestionRun.Start(
                contentHash: contentHash,
                fileName: request.FileName,
                contentType: request.ContentType,
                fileSizeBytes: bytes.LongLength,
                submittedBy: request.SubmittedBy,
                startedAt: DateTimeOffset.UtcNow);
            await runs.AddAsync(run, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to bootstrap ingestion run for {FileName}", request.FileName);
            return Result.Failure<IngestionRunSummary>(
                Error.Failure("Network.Ingestion.BootstrapFailed", ex.Message));
        }

        try
        {
            // ── Stage 1: Parse ────────────────────────────────────────────────
            Result<int> parseResult = await RunStageAsync(
                run, IngestionStatus.Parsing,
                () => sender.Send(new ParseNetworkLogCommand(
                    run.Id, request.ContentType, request.FileName,
                    new MemoryStream(bytes, writable: false)), cancellationToken),
                cancellationToken);
            if (parseResult.IsFailure)
            {
                return await FailAsync(run, parseResult.Error, cancellationToken);
            }

            // The orchestrator owns the run's EventsParsed state. The Stage-1 handler also
            // sets it on the EF-tracked entity, but doing it here keeps the orchestrator
            // independent of internal stage-handler implementation details.
            run.RecordParsedCount(parseResult.Value);

            // ── Stage 2: Analyze ─────────────────────────────────────────────
            Result<AiAnalysisResult> analysisResult = await RunStageAsync(
                run, IngestionStatus.Analyzing,
                () => sender.Send(new AnalyzeNetworkBatchCommand(run.Id, mcpFilePath), cancellationToken),
                cancellationToken);
            if (analysisResult.IsFailure)
            {
                return await FailAsync(run, analysisResult.Error, cancellationToken);
            }

            // ── Stage 3: Decide ──────────────────────────────────────────────
            Result<IReadOnlyList<PipelineAction>> decisionResult = await RunStageAsync(
                run, IngestionStatus.Deciding,
                () => sender.Send(new DecidePipelineActionsCommand(run.Id, analysisResult.Value), cancellationToken),
                cancellationToken);
            if (decisionResult.IsFailure)
            {
                return await FailAsync(run, decisionResult.Error, cancellationToken);
            }

            // ── Stage 4: Persist ─────────────────────────────────────────────
            Result<PipelineActionCounts> persistResult = await RunStageAsync(
                run, IngestionStatus.Persisting,
                () => sender.Send(new ApplyPipelineActionsCommand(run.Id, decisionResult.Value), cancellationToken),
                cancellationToken);
            if (persistResult.IsFailure)
            {
                return await FailAsync(run, persistResult.Error, cancellationToken);
            }

            // ── Stage 5: Project ─────────────────────────────────────────────
            // Hand the integration event to IEventBus, which queues it on the in-memory
            // channel. The IntegrationEventProcessorJob hosted service drains the channel
            // and re-publishes via MediatR — subscribers (dashboard projection, future
            // copilot KB indexer) run on that worker, decoupled from the orchestrator's
            // request lifetime. Slow / failing subscribers don't fail the run.
            DateTimeOffset projectStarted = DateTimeOffset.UtcNow;
            run.TransitionTo(IngestionStatus.Projecting);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            PipelineActionCounts counts = persistResult.Value;
            int anomaliesDetected = counts.AlertsCreated + counts.AlertsUpdated;

            IReadOnlyList<SiteSnapshotRecord> syncedSnapshots =
                await runs.ListSnapshotsAsync(run.Id, cancellationToken);

            await eventBus.PublishAsync(new PipelineCompletedNotification(
                Id: Guid.NewGuid(),
                IngestionRunId: run.Id,
                ContentHash: contentHash,
                FileName: request.FileName,
                EventsParsed: parseResult.Value,
                AnomaliesDetected: anomaliesDetected,
                AlertsCreated: counts.AlertsCreated,
                AlertsUpdated: counts.AlertsUpdated,
                OptimizationsCreated: counts.OptimizationsCreated,
                TopologyChanged: counts.TowerUpdates > 0 || counts.TowersCreated > 0,
                CompletedAt: DateTimeOffset.UtcNow,
                RecordsCreated: counts.TotalCreated,
                RecordsUpdated: counts.TotalUpdated,
                RecordsArchived: counts.TotalArchived,
                CriticalAlertsRaised: counts.AlertsCreated,
                WarningCount: counts.Warnings.Count,
                SubmittedBy: request.SubmittedBy,
                SiteCodes: syncedSnapshots.Select(s => s.SiteCode).Distinct().ToList()),
                cancellationToken);

            DateTimeOffset projectEnded = DateTimeOffset.UtcNow;
            run.RecordStageTiming(new StageTiming(
                IngestionStatus.Projecting, projectStarted, projectEnded,
                Succeeded: true, FailureReason: null));

            // ── Complete ─────────────────────────────────────────────────────
            run.Complete(
                new IngestionRunCounts(
                    AnomaliesDetected: anomaliesDetected,
                    AlertsCreated: counts.AlertsCreated,
                    AlertsUpdated: counts.AlertsUpdated,
                    OptimizationsCreated: counts.OptimizationsCreated,
                    TopologyChanged: counts.TowerUpdates > 0 || counts.TowersCreated > 0,
                    RecordsCreated: counts.TotalCreated,
                    RecordsUpdated: counts.TotalUpdated,
                    RecordsArchived: counts.TotalArchived,
                    TelemetryRowsAppended: counts.TelemetryRowsAppended,
                    Warnings: counts.Warnings),
                completedAt: DateTimeOffset.UtcNow);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Ingestion run {IngestionRunId} completed: {Created} created, {Updated} updated, " +
                "{Archived} archived, {Anomalies} anomalies, {Optimizations} optimizations, {Warnings} warning(s)",
                run.Id, counts.TotalCreated, counts.TotalUpdated, counts.TotalArchived,
                anomaliesDetected, counts.OptimizationsCreated, counts.Warnings.Count);

            IReadOnlyList<SiteSnapshotRecord> synced =
                await runs.ListSnapshotsAsync(run.Id, cancellationToken);

            return Result.Success(BuildSummary(run, deduplicatedFromPriorRun: false, synced));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ingestion run {IngestionRunId} threw an unhandled exception", run.Id);
            return await FailAsync(run,
                Error.Failure("Network.Ingestion.UnhandledException", ex.Message),
                cancellationToken);
        }
    }

    /// <summary>
    /// Transitions the run into <paramref name="stage"/>, stamps the start time, dispatches
    /// the stage's command via the supplied invoker, then records the resulting StageTiming
    /// (success or failure). The orchestrator owns the transition so per-stage handlers stay
    /// pure use-cases.
    /// </summary>
    private async Task<Result<T>> RunStageAsync<T>(
        IngestionRun run,
        IngestionStatus stage,
        Func<Task<Result<T>>> invoker,
        CancellationToken cancellationToken)
    {
        run.TransitionTo(stage);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        DateTimeOffset start = DateTimeOffset.UtcNow;

        try
        {
            Result<T> result = await invoker();
            string? failureReason = result.IsFailure
                ? $"{result.Error.Code}: {result.Error.Description}"
                : null;

            run.RecordStageTiming(new StageTiming(
                stage, start, DateTimeOffset.UtcNow,
                Succeeded: failureReason is null,
                FailureReason: failureReason));

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.RecordStageTiming(new StageTiming(
                stage, start, DateTimeOffset.UtcNow,
                Succeeded: false,
                FailureReason: $"unhandled: {ex.Message}"));
            throw;
        }
    }

    private async Task<Result<IngestionRunSummary>> FailAsync(
        IngestionRun run, Error error, CancellationToken cancellationToken)
    {
        run.Fail($"{error.Code}: {error.Description}", DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogError(
            "Ingestion run {IngestionRunId} failed: {ErrorCode} {ErrorDescription}",
            run.Id, error.Code, error.Description);

        // A failed run publishes no completion event, so without this a synchronisation failure would
        // be silent to everything downstream — the operator would see the feed simply stop landing.
        // Publishing must not itself fail the run: the caller already has the error, and losing the
        // notification is strictly better than masking the real failure with a second one.
        try
        {
            await eventBus.PublishAsync(new PipelineFailedNotification(
                Id: Guid.NewGuid(),
                IngestionRunId: run.Id,
                FileName: run.FileName,
                Reason: $"{error.Code}: {error.Description}",
                SubmittedBy: run.SubmittedBy,
                FailedAt: DateTimeOffset.UtcNow), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to publish the failure notification for run {IngestionRunId}", run.Id);
        }

        return Result.Failure<IngestionRunSummary>(error);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment))
        {
            byte[] copy = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array!, segment.Offset, copy, 0, segment.Count);
            return copy;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>
    /// Projects a run onto the report the caller sees. Shared by the live path and the
    /// short-circuit path so a deduplicated re-upload reports exactly what the original run did —
    /// the user gets the same report either way, plus the flag telling them why nothing changed.
    /// </summary>
    public static IngestionRunSummary BuildSummary(
        IngestionRun run,
        bool deduplicatedFromPriorRun,
        IReadOnlyList<SiteSnapshotRecord>? snapshots = null) =>
        new(
            IngestionRunId: run.Id,
            ContentHash: run.ContentHash,
            FinalStatus: run.Status,
            EventsParsed: run.EventsParsed,
            AnomaliesDetected: run.AnomaliesDetected,
            AlertsCreated: run.AlertsCreated,
            AlertsUpdated: run.AlertsUpdated,
            OptimizationsCreated: run.OptimizationsCreated,
            TopologyChanged: run.TopologyChanged,
            DeduplicatedFromPriorRun: deduplicatedFromPriorRun,
            StageTimings: run.StageTimings,
            FailureReason: run.FailureReason,
            RecordsCreated: run.RecordsCreated,
            RecordsUpdated: run.RecordsUpdated,
            RecordsArchived: run.RecordsArchived,
            TelemetryRowsAppended: run.TelemetryRowsAppended,
            Warnings: SplitWarnings(run.Warnings),
            SyncedSites: (snapshots ?? []).Select(ToSyncedSite).ToList(),
            FileName: run.FileName,
            SubmittedBy: run.SubmittedBy,
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            DurationMs: run.Duration?.TotalMilliseconds);

    private static IReadOnlyList<string> SplitWarnings(string? warnings) =>
        string.IsNullOrWhiteSpace(warnings)
            ? []
            : warnings.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static SyncedSiteSummary ToSyncedSite(SiteSnapshotRecord s) =>
        new(
            SiteCode: s.SiteCode,
            SiteName: s.SiteName,
            SiteId: s.SiteId,
            Region: s.Region,
            Provider: s.Provider,
            Environment: s.Environment,
            Vendor: s.Vendor,
            Technologies: s.Technologies,
            HealthScore: s.HealthScore,
            RequestId: s.RequestId,
            SnapshotVersion: s.SnapshotVersion,
            GeneratedAt: s.GeneratedAt,
            CapturedAt: s.CapturedAt);
}
