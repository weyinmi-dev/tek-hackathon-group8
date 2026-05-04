namespace Web.Api.Endpoints.Geo;

/// <summary>
/// OSM-derived geo context attached to every site-keyed list item the API returns
/// (alerts, sites, anomalies). Computed once per site per cache TTL via
/// <c>ISiteGeoLookup</c> → <c>OsmClient</c> (Nominatim + Overpass), so the LLM and
/// the UI see the same answer without re-hitting OSM on every request.
///
/// Wire shape — kept flat so the frontend can render a small badge directly without
/// a mapper. <c>NearestFuelStationMetres</c> is null when no station is found within
/// the 15km Overpass search radius.
/// </summary>
public sealed record GeoSummary(
    double Latitude,
    double Longitude,
    string RegionType,             // "urban" | "suburban" | "rural" | "remote"
    double AccessibilityScore,     // 0-100, higher = better access / denser infrastructure
    int? NearestFuelStationMetres,
    string? NearestFuelStationName,
    string? Address);              // Nominatim display_name, may be null on lookup failure
