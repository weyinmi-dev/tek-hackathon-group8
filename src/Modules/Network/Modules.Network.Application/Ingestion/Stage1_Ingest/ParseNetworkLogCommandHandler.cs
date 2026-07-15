using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;
using Modules.Network.Domain;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage1_Ingest;

internal sealed class ParseNetworkLogCommandHandler(
    IIngestionRunRepository runs,
    INetworkLogParserRegistry registry,
    IUnitOfWork unitOfWork,
    ILogger<ParseNetworkLogCommandHandler> logger)
    : ICommandHandler<ParseNetworkLogCommand, int>
{
    public async Task<Result<int>> Handle(ParseNetworkLogCommand request, CancellationToken cancellationToken)
    {
        IngestionRun? run = await runs.GetByIdAsync(request.IngestionRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure<int>(Error.NotFound(
                "Network.Ingestion.RunNotFound",
                $"Ingestion run {request.IngestionRunId} not found."));
        }

        if (run.Status != IngestionStatus.Parsing)
        {
            return Result.Failure<int>(Error.Conflict(
                "Network.Ingestion.WrongStage",
                $"Run {run.Id} is in {run.Status}, not Parsing — orchestrator must transition first."));
        }

        Result<INetworkLogParser> parserResult = registry.Resolve(request.ContentType, request.FileName);
        if (parserResult.IsFailure)
        {
            return Result.Failure<int>(parserResult.Error);
        }

        INetworkLogParser parser = parserResult.Value;
        logger.LogInformation(
            "Parsing run {IngestionRunId} as {Format} ({ContentType}, {FileName})",
            run.Id, parser.Format, request.ContentType, request.FileName);

        Result<NetworkLogParseResult> parseResult =
            await parser.ParseAsync(run.Id, request.Content, cancellationToken);

        if (parseResult.IsFailure)
        {
            return Result.Failure<int>(parseResult.Error);
        }

        IReadOnlyList<NetworkEvent> events = parseResult.Value.Events;
        IReadOnlyList<SiteSnapshotPayload> snapshots = parseResult.Value.Snapshots;

        await runs.AddEventsAsync(events, cancellationToken);

        if (snapshots.Count > 0)
        {
            // Store the snapshot documents verbatim. They are the evidence of what the feed
            // reported and the input Stage 3 plans the synchronisation from — persisting them
            // here (rather than passing them in memory) means a stage retry replays the same
            // document, and gives the file index and telemetry trends a durable source.
            await runs.AddSnapshotsAsync(
                snapshots.Select(s => ToRecord(run.Id, s)).ToList(),
                cancellationToken);

            logger.LogInformation(
                "Run {IngestionRunId} carried {SnapshotCount} site snapshot(s): {SiteCodes}",
                run.Id, snapshots.Count, string.Join(", ", snapshots.Select(s => s.Site.SiteCode)));
        }

        run.RecordParsedCount(events.Count);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(events.Count);
    }

    /// <summary>
    /// Flattens the canonical payload onto the persistence record. The typed payload lives in the
    /// shared pipeline abstractions, which Network.Domain cannot reference — so the mapping to
    /// primitives happens here, in Application, and the domain keeps the document as raw JSON.
    /// </summary>
    private static SiteSnapshotRecord ToRecord(Guid runId, SiteSnapshotPayload payload) =>
        SiteSnapshotRecord.Create(
            ingestionRunId: runId,
            requestId: payload.RequestId ?? string.Empty,
            provider: payload.Provider ?? "Unknown",
            environment: payload.Environment ?? "Unknown",
            siteId: payload.Site.SiteId ?? payload.Site.SiteCode,
            siteCode: payload.Site.SiteCode,
            siteName: payload.Site.SiteName ?? payload.Site.SiteCode,
            region: payload.Site.Region ?? "Unknown",
            vendor: payload.Site.Vendor,
            technologies: string.Join(',', payload.Site.Technology),
            healthScore: payload.Site.HealthScore,
            latitude: payload.Site.Latitude,
            longitude: payload.Site.Longitude,
            generatedAt: payload.GeneratedAt,
            capturedAt: payload.Performance?.CapturedAt,
            lastHeartbeat: payload.Site.LastHeartbeat,
            snapshotVersion: SiteSnapshotPayload.CurrentVersion,
            rawJson: payload.Serialize());
}
