using Application.Abstractions.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
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
    IPublisher publisher,
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

        // 2. File-level idempotency. Re-uploading the same bytes resolves to the original run.
        IngestionRun? prior = await runs.GetByContentHashAsync(contentHash, cancellationToken);
        if (prior is not null)
        {
            logger.LogInformation(
                "Ingestion short-circuited — content hash {ContentHash} already processed as run {IngestionRunId}",
                contentHash, prior.Id);
            return Result.Success(BuildSummary(prior, deduplicatedFromPriorRun: true));
        }

        // 3. Create the run. SaveChanges immediately so subsequent stage handlers can find it by ID.
        var run = IngestionRun.Start(
            contentHash: contentHash,
            fileName: request.FileName,
            contentType: request.ContentType,
            fileSizeBytes: bytes.LongLength,
            submittedBy: request.SubmittedBy,
            startedAt: DateTimeOffset.UtcNow);
        await runs.AddAsync(run, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

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
                () => sender.Send(new AnalyzeNetworkBatchCommand(run.Id), cancellationToken),
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
            // Publish notification under the Projecting status; subscribers update read models,
            // KB index, etc. Failures inside subscribers are best-effort and don't fail the run.
            DateTimeOffset projectStarted = DateTimeOffset.UtcNow;
            run.TransitionTo(IngestionStatus.Projecting);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            PipelineActionCounts counts = persistResult.Value;
            int anomaliesDetected = counts.AlertsCreated + counts.AlertsUpdated;
            try
            {
                await publisher.Publish(new PipelineCompletedNotification(
                    IngestionRunId: run.Id,
                    ContentHash: contentHash,
                    FileName: request.FileName,
                    EventsParsed: parseResult.Value,
                    AnomaliesDetected: anomaliesDetected,
                    AlertsCreated: counts.AlertsCreated,
                    AlertsUpdated: counts.AlertsUpdated,
                    OptimizationsCreated: counts.OptimizationsCreated,
                    TopologyChanged: counts.TowerUpdates > 0,
                    CompletedAt: DateTimeOffset.UtcNow), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Pipeline projection notification handler failed for run {IngestionRunId} — run will still complete",
                    run.Id);
            }

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
                    TopologyChanged: counts.TowerUpdates > 0),
                completedAt: DateTimeOffset.UtcNow);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Ingestion run {IngestionRunId} completed: {Anomalies} anomalies, " +
                "{AlertsCreated}/{AlertsUpdated} alerts created/updated, {Optimizations} optimizations, {TowerUpdates} tower updates",
                run.Id, anomaliesDetected, counts.AlertsCreated, counts.AlertsUpdated,
                counts.OptimizationsCreated, counts.TowerUpdates);

            return Result.Success(BuildSummary(run, deduplicatedFromPriorRun: false));
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

    private static IngestionRunSummary BuildSummary(IngestionRun run, bool deduplicatedFromPriorRun) =>
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
            FailureReason: run.FailureReason);
}
