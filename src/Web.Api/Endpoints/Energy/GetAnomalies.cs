using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Modules.Energy.Application.Anomalies.GetAnomalies;
using SharedKernel;
using Web.Api.Endpoints.Geo;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetAnomalies : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/energy/anomalies → recent anomaly detections with OSM geo context
        // attached per item. The geo data lets the UI tag suspected fuel-theft events
        // at remote sites with high accessibility risk, matching the directive's
        // example flow ("Fuel drop at Site A. Location is remote with low infrastructure
        // density and poor accessibility — high probability of fuel theft.").
        //
        // Geo is best-effort: failures degrade to geo=null per item rather than 500ing
        // the endpoint. See GeoEnricher's class doc for the full resilience contract.
        app.MapGet("energy/anomalies", [Authorize] async (
            int? take, ISender sender, GeoEnricher geo,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            Result<AnomaliesResponse> result = await sender.Send(new GetAnomaliesQuery(take ?? 50), ct);
            if (result.IsFailure) return CustomResults.Problem(result);

            IReadOnlyList<AnomalyDto> anomalies = result.Value.Anomalies;

            IReadOnlyDictionary<string, GeoSummary> geoMap;
            try
            {
                geoMap = await geo.ForSitesAsync(anomalies.Select(a => a.Site), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Energy.GetAnomalies").LogWarning(
                    ex, "Geo batch failed; serving anomalies without geo context.");
                geoMap = new Dictionary<string, GeoSummary>(StringComparer.OrdinalIgnoreCase);
            }

            List<AnomalyWithGeo> enriched = anomalies
                .Select(a => AnomalyWithGeo.From(a, geoMap.GetValueOrDefault(a.Site)))
                .ToList();
            return Results.Ok(new AnomaliesWithGeoResponse(enriched));
        })
        .WithTags(Tags.Energy);
    }
}
