using Application.Abstractions.Messaging;
using Modules.Network.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools;

// Synchronisation tool queries. Same shape as the other tool queries: a thin MediatR query over
// INetworkApi, so the agent dispatches through the standard application pipeline and never touches
// another module's application layer or repositories.

/// <summary>get_site_detail — the full current state of a site as of its latest synchronised snapshot.</summary>
public sealed record GetSiteSyncStateQuery(string SiteCode) : IQuery<SiteSyncState>;

internal sealed class GetSiteSyncStateQueryHandler(INetworkApi network)
    : IQueryHandler<GetSiteSyncStateQuery, SiteSyncState>
{
    public async Task<Result<SiteSyncState>> Handle(GetSiteSyncStateQuery request, CancellationToken cancellationToken)
    {
        SiteSyncState? state = await network.GetSiteSyncStateAsync(request.SiteCode, cancellationToken);

        return state is null
            ? Result.Failure<SiteSyncState>(Error.NotFound(
                "Site.NotFound", $"No site with code '{request.SiteCode}'."))
            : Result.Success(state);
    }
}

/// <summary>get_site_telemetry — the reported history of a site over a time range.</summary>
public sealed record GetSiteTelemetryHistoryQuery(string SiteCode, int Hours)
    : IQuery<IReadOnlyList<SiteTelemetrySample>>;

internal sealed class GetSiteTelemetryHistoryQueryHandler(INetworkApi network)
    : IQueryHandler<GetSiteTelemetryHistoryQuery, IReadOnlyList<SiteTelemetrySample>>
{
    public async Task<Result<IReadOnlyList<SiteTelemetrySample>>> Handle(
        GetSiteTelemetryHistoryQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<SiteTelemetrySample> samples = await network.GetSiteTelemetryAsync(
            request.SiteCode, request.Hours <= 0 ? 24 : request.Hours, cancellationToken);

        return Result.Success(samples);
    }
}

/// <summary>get_sync_report — what one upload created, updated and archived.</summary>
public sealed record GetSyncReportQuery(Guid IngestionRunId) : IQuery<SyncRunSummary>;

internal sealed class GetSyncReportQueryHandler(INetworkApi network)
    : IQueryHandler<GetSyncReportQuery, SyncRunSummary>
{
    public async Task<Result<SyncRunSummary>> Handle(GetSyncReportQuery request, CancellationToken cancellationToken)
    {
        SyncRunSummary? run = await network.GetSyncRunAsync(request.IngestionRunId, cancellationToken);

        return run is null
            ? Result.Failure<SyncRunSummary>(Error.NotFound(
                "IngestionRun.NotFound", $"No upload with id '{request.IngestionRunId}'."))
            : Result.Success(run);
    }
}

/// <summary>list_recent_uploads — the synchronisation history, newest first.</summary>
public sealed record ListRecentUploadsQuery(string? SiteCode, int Take)
    : IQuery<IReadOnlyList<SyncRunSummary>>;

internal sealed class ListRecentUploadsQueryHandler(INetworkApi network)
    : IQueryHandler<ListRecentUploadsQuery, IReadOnlyList<SyncRunSummary>>
{
    public async Task<Result<IReadOnlyList<SyncRunSummary>>> Handle(
        ListRecentUploadsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<SyncRunSummary> runs = await network.ListSyncRunsAsync(
            string.IsNullOrWhiteSpace(request.SiteCode) ? null : request.SiteCode,
            request.Take <= 0 ? 10 : request.Take,
            cancellationToken);

        return Result.Success(runs);
    }
}
