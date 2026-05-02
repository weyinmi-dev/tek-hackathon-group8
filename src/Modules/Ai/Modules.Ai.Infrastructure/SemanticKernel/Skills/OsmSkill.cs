using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Modules.Ai.Infrastructure.Mcp.Osm;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill that exposes OpenStreetMap geospatial intelligence to auto-function-calling.
/// Each function maps 1:1 to the OSM MCP plugin so the LLM picks the same capability whether
/// it's invoked through the orchestrator (here) or the operator's MCP-discovery endpoint.
///
/// Per the directive's "Tool selection rule":
///   • Use this skill when the user query needs spatial reasoning (where, accessibility,
///     proximity, region type, dispatch reach).
///   • Combine with KnowledgeSkill (RAG) when the answer also needs historical "why" — e.g.
///     "Has fuel theft happened before at sites this remote?".
///   • Combine with EnergySkill when the answer needs current energy state at the resolved
///     coordinates — e.g. "Find the most remote diesel-only sites".
/// </summary>
public sealed class OsmSkill(IOsmClient osm, ISiteGeoLookup geo)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    [KernelFunction("osm_get_nearby_infrastructure")]
    [Description("Tool: osm_get_nearby_infrastructure. List infrastructure (roads, hospitals, fuel stations, schools, shops) within a radius of a coordinate. Use to assess what's around a location for accessibility, dispatch, or risk reasoning. Returns items sorted by distance.")]
    public async Task<string> GetNearbyInfrastructureAsync(
        [Description("Latitude in decimal degrees.")] double lat,
        [Description("Longitude in decimal degrees.")] double lon,
        [Description("Search radius in metres (default 2000, max 10000).")] int radiusMetres = 2000,
        CancellationToken ct = default)
    {
        int radius = Math.Clamp(radiusMetres, 100, 10_000);
        OsmNearbyInfrastructure result = await osm.GetNearbyInfrastructureAsync(lat, lon, radius, ct);
        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [KernelFunction("osm_get_distance_to_fuel_station")]
    [Description("Tool: osm_get_distance_to_fuel_station. Straight-line distance (metres) and identity of the nearest fuel station to a coordinate. Use for refuel-logistics reasoning and theft probability — sites far from fuel stations are higher-risk targets.")]
    public async Task<string> GetDistanceToFuelStationAsync(
        [Description("Latitude in decimal degrees.")] double lat,
        [Description("Longitude in decimal degrees.")] double lon,
        CancellationToken ct = default)
    {
        OsmFuelStationDistance result = await osm.GetDistanceToFuelStationAsync(lat, lon, ct);
        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [KernelFunction("osm_classify_region")]
    [Description("Tool: osm_classify_region. Classify a coordinate as urban / suburban / rural / remote based on building, road, and amenity density in a 1km radius. Returns an accessibility score 0-100 (higher = better access). Use to reason about site isolation and theft / fault response time.")]
    public async Task<string> ClassifyRegionAsync(
        [Description("Latitude in decimal degrees.")] double lat,
        [Description("Longitude in decimal degrees.")] double lon,
        CancellationToken ct = default)
    {
        OsmRegionClassification result = await osm.ClassifyRegionAsync(lat, lon, ct);
        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [KernelFunction("osm_calculate_route_distance")]
    [Description("Tool: osm_calculate_route_distance. Straight-line (haversine) distance in metres between two coordinates. Use as a proxy for dispatch reach — e.g. how far is the nearest depot from a site at risk.")]
    public async Task<string> CalculateRouteDistanceAsync(
        [Description("Origin latitude.")] double originLat,
        [Description("Origin longitude.")] double originLon,
        [Description("Destination latitude.")] double destinationLat,
        [Description("Destination longitude.")] double destinationLon,
        CancellationToken ct = default)
    {
        OsmRouteDistance result = await osm.CalculateRouteDistanceAsync(originLat, originLon, destinationLat, destinationLon, ct);
        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [KernelFunction("osm_reverse_geocode")]
    [Description("Tool: osm_reverse_geocode. Resolve a coordinate to a postal-style address (country, state, city, road). Use to humanise raw lat/lon when explaining a site's location.")]
    public async Task<string> ReverseGeocodeAsync(
        [Description("Latitude in decimal degrees.")] double lat,
        [Description("Longitude in decimal degrees.")] double lon,
        CancellationToken ct = default)
    {
        OsmPlace? place = await osm.ReverseGeocodeAsync(lat, lon, ct);
        return place is null
            ? JsonSerializer.Serialize(new { error = "no_place", message = "No place data returned." }, JsonOpts)
            : JsonSerializer.Serialize(place, JsonOpts);
    }

    [KernelFunction("osm_get_site_geocontext")]
    [Description("Tool: osm_get_site_geocontext. Composite geo-context for a site by code: coordinates, address, region classification (urban/rural), accessibility score, and nearest fuel station. Prefer this single call over invoking the four primitive OSM tools separately when the user asks about a specific site by code.")]
    public async Task<string> GetSiteGeoContextAsync(
        [Description("Tower / site code, e.g. 'TWR-LEK-003'.")] string siteCode,
        CancellationToken ct = default)
    {
        SiteGeoContext? ctx = await geo.GetAsync(siteCode ?? "", ct);
        return ctx is null
            ? JsonSerializer.Serialize(new { error = "site_not_found", message = $"Site '{siteCode}' not found or has no coordinates on file." }, JsonOpts)
            : JsonSerializer.Serialize(ctx, JsonOpts);
    }

    [KernelFunction("osm_get_site_nearby")]
    [Description("Tool: osm_get_site_nearby. Nearby infrastructure for a site by code (resolves coordinates first, then queries OSM). Use when the user asks 'what's around site X' or 'is X accessible'.")]
    public async Task<string> GetSiteNearbyAsync(
        [Description("Tower / site code.")] string siteCode,
        [Description("Search radius in metres (default 2000, max 10000).")] int radiusMetres = 2000,
        CancellationToken ct = default)
    {
        (double Lat, double Lon)? coords = await geo.GetCoordinatesAsync(siteCode ?? "", ct);
        if (coords is null)
        {
            return JsonSerializer.Serialize(new { error = "site_not_found", message = $"Site '{siteCode}' has no coordinates on file." }, JsonOpts);
        }
        int radius = Math.Clamp(radiusMetres, 100, 10_000);
        OsmNearbyInfrastructure result = await osm.GetNearbyInfrastructureAsync(coords.Value.Lat, coords.Value.Lon, radius, ct);
        return JsonSerializer.Serialize(result, JsonOpts);
    }
}
