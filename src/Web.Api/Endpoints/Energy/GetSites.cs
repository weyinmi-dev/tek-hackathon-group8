using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Modules.Energy.Application.Sites.GetSites;
using SharedKernel;
using Web.Api.Endpoints.Geo;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetSites : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/energy/sites → fleet table for the Energy Sites page.
        // Each site is enriched with OSM geo context (lat/lon, region type, accessibility,
        // nearest fuel station) so the frontend can flag remote / poorly-served sites
        // without extra round-trips. Open to every authenticated user.
        //
        // Geo is best-effort decoration: see GeoEnricher's class doc for the resilience
        // contract. The endpoint will always return the underlying site list even if OSM
        // / Redis / the network module's tower lookup is unavailable.
        app.MapGet("energy/sites", [Authorize] async (
            ISender sender, GeoEnricher geo, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            Result<SitesResponse> result = await sender.Send(new GetSitesQuery(), ct);
            if (result.IsFailure) return CustomResults.Problem(result);

            IReadOnlyList<SiteDto> sites = result.Value.Sites;

            IReadOnlyDictionary<string, GeoSummary> geoMap;
            try
            {
                geoMap = await geo.ForSitesAsync(sites.Select(s => s.Id), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Energy.GetSites").LogWarning(
                    ex, "Geo batch failed; serving sites without geo context.");
                geoMap = new Dictionary<string, GeoSummary>(StringComparer.OrdinalIgnoreCase);
            }

            List<SiteWithGeo> enriched = sites
                .Select(s => SiteWithGeo.From(s, geoMap.GetValueOrDefault(s.Id)))
                .ToList();
            return Results.Ok(new SitesWithGeoResponse(enriched));
        })
        .WithTags(Tags.Energy);
    }
}
