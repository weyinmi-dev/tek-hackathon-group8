namespace Modules.Ai.Infrastructure.Mcp.Osm;

/// <summary>
/// Structured OSM responses returned to MCP / SK callers. Records are intentionally
/// simple JSON-friendly DTOs so the LLM can quote the fields verbatim and the
/// frontend can render them without a separate mapper.
/// </summary>
public sealed record OsmCoordinates(double Latitude, double Longitude);

public sealed record OsmPlace(
    string DisplayName,
    string? Country,
    string? State,
    string? City,
    string? Suburb,
    string? Road,
    OsmCoordinates Coordinates);

public sealed record OsmInfrastructureItem(
    string Category,
    string Name,
    string? Subtype,
    OsmCoordinates Coordinates,
    double DistanceMetres);

public sealed record OsmNearbyInfrastructure(
    OsmCoordinates Origin,
    int RadiusMetres,
    int TotalCount,
    IReadOnlyList<OsmInfrastructureItem> Items);

public sealed record OsmFuelStationDistance(
    OsmCoordinates Origin,
    bool Found,
    string? Name,
    OsmCoordinates? StationCoordinates,
    double? StraightLineMetres,
    string? Brand);

public sealed record OsmRegionClassification(
    OsmCoordinates Origin,
    string RegionType,         // "urban" | "suburban" | "rural" | "remote"
    int BuildingCount,
    int AmenityCount,
    int RoadSegmentCount,
    double AccessibilityScore, // 0-100, higher = better access / closer to dense infrastructure
    string Reasoning);

public sealed record OsmRouteDistance(
    OsmCoordinates Origin,
    OsmCoordinates Destination,
    double StraightLineMetres,
    string Method);            // "haversine" — real road-routing requires OSRM/Mapbox

/// <summary>
/// Composite geo-context for a single Site. Built once and cached; reused by alerts,
/// optimization, and Copilot reasoning so we don't hit OSM on every request.
/// </summary>
public sealed record SiteGeoContext(
    string SiteCode,
    OsmCoordinates Coordinates,
    OsmPlace? Place,
    OsmRegionClassification Classification,
    OsmFuelStationDistance NearestFuelStation,
    DateTime ComputedAtUtc);
