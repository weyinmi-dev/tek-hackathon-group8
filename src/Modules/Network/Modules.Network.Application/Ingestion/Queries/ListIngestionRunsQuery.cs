using Application.Abstractions.Messaging;
using Modules.Network.Application.Ingestion.Pipeline;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Queries;

/// <summary>
/// The synchronisation history: one entry per upload. Backs both tabs of the sync page — the RUNS
/// tab reads the counts and status, the FILES tab reads the file metadata and provenance — because
/// they are two views of the same records, not two datasets.
/// </summary>
public sealed record ListIngestionRunsQuery(
    string? SiteCode = null,
    string? Provider = null,
    string? Search = null,
    int Skip = 0,
    int Take = 25) : IQuery<IngestionRunPage>;

public sealed record IngestionRunPage(IReadOnlyList<IngestionRunSummary> Runs, int Total);

internal sealed class ListIngestionRunsQueryHandler(IIngestionRunRepository runs)
    : IQueryHandler<ListIngestionRunsQuery, IngestionRunPage>
{
    public async Task<Result<IngestionRunPage>> Handle(
        ListIngestionRunsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<IngestionRun> page = await runs.SearchRunsAsync(
            request.SiteCode, request.Provider, request.Search,
            request.Skip, request.Take, cancellationToken);

        int total = await runs.CountRunsAsync(
            request.SiteCode, request.Provider, request.Search, cancellationToken);

        var summaries = new List<IngestionRunSummary>(page.Count);
        foreach (IngestionRun run in page)
        {
            IReadOnlyList<SiteSnapshotRecord> snapshots =
                await runs.ListSnapshotsAsync(run.Id, cancellationToken);

            summaries.Add(ProcessNetworkLogCommandHandler.BuildSummary(
                run, deduplicatedFromPriorRun: false, snapshots));
        }

        return Result.Success(new IngestionRunPage(summaries, total));
    }
}

/// <summary>The detailed report for one upload, shown when an entry in the history is selected.</summary>
public sealed record GetIngestionRunQuery(Guid IngestionRunId) : IQuery<IngestionRunSummary>;

internal sealed class GetIngestionRunQueryHandler(IIngestionRunRepository runs)
    : IQueryHandler<GetIngestionRunQuery, IngestionRunSummary>
{
    public async Task<Result<IngestionRunSummary>> Handle(
        GetIngestionRunQuery request, CancellationToken cancellationToken)
    {
        IngestionRun? run = await runs.GetByIdAsync(request.IngestionRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure<IngestionRunSummary>(Error.NotFound(
                "Network.Ingestion.RunNotFound",
                $"Ingestion run {request.IngestionRunId} not found."));
        }

        IReadOnlyList<SiteSnapshotRecord> snapshots =
            await runs.ListSnapshotsAsync(run.Id, cancellationToken);

        return Result.Success(ProcessNetworkLogCommandHandler.BuildSummary(
            run, deduplicatedFromPriorRun: false, snapshots));
    }
}
