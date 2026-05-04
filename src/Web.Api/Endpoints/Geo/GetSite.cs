using Microsoft.AspNetCore.Authorization;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Geo;

/// <summary>
/// On-demand single-site OSM lookup used by the network map page when an operator
/// selects a tower. The map list endpoint deliberately does not bulk-enrich every
/// tower — Overpass is rate-limited and most towers are never inspected — so the
/// frontend fetches geo per selection. Once warmed, results live in Redis for 24h
/// (see <c>CachedOsmClient</c>) and subsequent selections of the same tower
/// resolve in single-digit ms.
///
/// Returns 200 with a <see cref="GeoSummary"/>, or 200 with <c>null</c> body when
/// the lookup is unavailable. Geo is decoration; a 5xx here would block the tower
/// detail panel from rendering basic metrics, which is the wrong tradeoff.
/// </summary>
public sealed class GetSite : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/geo/sites/{siteCode}
        app.MapGet("geo/sites/{siteCode}", [Authorize] async (
            string siteCode, GeoEnricher geo, CancellationToken ct) =>
        {
            GeoSummary? summary = await geo.ForSiteAsync(siteCode, ct);
            return Results.Ok(summary);
        })
        .WithTags(Tags.Geo);
    }
}
