using Application.Abstractions.Messaging;
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

        Result<IReadOnlyList<NetworkEvent>> parseResult =
            await parser.ParseAsync(run.Id, request.Content, cancellationToken);

        if (parseResult.IsFailure)
        {
            return Result.Failure<int>(parseResult.Error);
        }

        IReadOnlyList<NetworkEvent> events = parseResult.Value;

        await runs.AddEventsAsync(events, cancellationToken);
        run.RecordParsedCount(events.Count);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(events.Count);
    }
}
