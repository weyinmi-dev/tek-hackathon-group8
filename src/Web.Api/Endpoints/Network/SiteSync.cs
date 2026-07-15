using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Network.Application.Ingestion.Pipeline;
using Modules.Network.Application.Ingestion.Queries;
using Modules.Network.Application.Sites;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Network;

/// <summary>
/// Read side of synchronisation: the upload history, the current state of a site, and its historical
/// telemetry. Thin — every one of these is a single query dispatch, with all the work in the
/// Application layer.
/// </summary>
public sealed class SiteSync : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // The synchronisation history. Feeds both tabs of the sync page — RUNS reads the counts,
        // FILES reads the file metadata — because they are two views of one record set.
        app.MapGet("network/ingestion-runs", [Authorize]
            async (string? siteCode, string? provider, string? search, int? skip, int? take,
                   ISender sender, CancellationToken ct) =>
        {
            Result<IngestionRunPage> result = await sender.Send(
                new ListIngestionRunsQuery(siteCode, provider, search, skip ?? 0, take ?? 25), ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.NetworkIngestion);

        // The full synchronisation report for one upload.
        app.MapGet("network/ingestion-runs/{id:guid}", [Authorize]
            async (Guid id, ISender sender, CancellationToken ct) =>
        {
            Result<IngestionRunSummary> result = await sender.Send(new GetIngestionRunQuery(id), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.NetworkIngestion);

        // The latest synchronised state of a site — what the Site Details page renders.
        app.MapGet("network/sites/{code}", [Authorize]
            async (string code, ISender sender, CancellationToken ct) =>
        {
            Result<SiteDetail> result = await sender.Send(new GetSiteDetailQuery(code), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.NetworkIngestion);

        // Historical telemetry for the trend charts. `hours` is the time-range filter.
        app.MapGet("network/sites/{code}/telemetry", [Authorize]
            async (string code, int? hours, ISender sender, CancellationToken ct) =>
        {
            Result<SiteTelemetry> result = await sender.Send(new GetSiteTelemetryQuery(code, hours ?? 24), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.NetworkIngestion);
    }
}
