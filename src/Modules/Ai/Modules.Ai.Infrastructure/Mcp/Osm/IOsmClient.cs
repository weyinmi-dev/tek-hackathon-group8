namespace Modules.Ai.Infrastructure.Mcp.Osm;

/// <summary>
/// Provider-neutral OSM contract. The HTTP-backed implementation hits Nominatim + Overpass
/// (the same APIs the upstream open-streetmap-mcp Python server uses). A caching decorator
/// fronts every method so identical (lat,lon[,radius]) inputs hit Redis instead of OSM.
/// </summary>
public interface IOsmClient
{
    Task<OsmPlace?> ReverseGeocodeAsync(double lat, double lon, CancellationToken ct = default);

    Task<OsmNearbyInfrastructure> GetNearbyInfrastructureAsync(
        double lat,
        double lon,
        int radiusMetres,
        CancellationToken ct = default);

    Task<OsmFuelStationDistance> GetDistanceToFuelStationAsync(
        double lat,
        double lon,
        CancellationToken ct = default);

    Task<OsmRegionClassification> ClassifyRegionAsync(
        double lat,
        double lon,
        CancellationToken ct = default);

    Task<OsmRouteDistance> CalculateRouteDistanceAsync(
        double originLat,
        double originLon,
        double destinationLat,
        double destinationLon,
        CancellationToken ct = default);
}
