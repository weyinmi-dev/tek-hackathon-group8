using System.Globalization;
using Modules.Ai.Application.Mcp.Contracts;
using Modules.Ai.Infrastructure.Mcp.Osm;

namespace Modules.Ai.Infrastructure.Mcp.Plugins;

/// <summary>
/// MCP plugin that exposes OpenStreetMap geospatial intelligence to the registry, the operator
/// /api/mcp/invoke endpoint, and the Copilot. Mirrors the surface of the upstream Python project
/// at https://github.com/jagan-shanmugam/open-streetmap-mcp — same capability names so any prompt
/// or runbook authored against that server transfers verbatim.
///
/// Capabilities are read-only and bound to OSM's public APIs (Nominatim, Overpass) plus a
/// haversine route distance. Every call goes through the cached client so identical
/// (lat, lon[, radius]) inputs hit Redis instead of OSM, satisfying the directive's
/// "Avoid repeated MCP calls for the same data" rule.
///
/// Site-keyed entry points (<c>get_site_geocontext</c>, <c>get_site_nearby</c>) translate a
/// site_code → tower coordinates → OSM, so the LLM can reason "How accessible is TWR-LEK-003?"
/// without knowing the underlying lat/lon.
/// </summary>
internal sealed class OsmMcpPlugin(IOsmClient osm, ISiteGeoLookup geo) : IMcpPlugin
{
    public string PluginId => "osm";
    public string DisplayName => "OpenStreetMap Geospatial";
    public McpPluginKind Kind => McpPluginKind.Internal;

    public IReadOnlyList<McpCapability> Capabilities { get; } =
    [
        new McpCapability(
            "get_nearby_infrastructure",
            "List infrastructure (roads, hospitals, fuel stations, schools, shops, amenities) within " +
            "a given radius of a coordinate. Use to assess what's around a site for accessibility, " +
            "logistics, or risk reasoning.",
            [
                new McpCapabilityParameter("lat", "number", "Latitude in decimal degrees.", IsRequired: true),
                new McpCapabilityParameter("lon", "number", "Longitude in decimal degrees.", IsRequired: true),
                new McpCapabilityParameter("radius_metres", "integer", "Search radius in metres (default 2000).", IsRequired: false),
            ]),
        new McpCapability(
            "get_distance_to_fuel_station",
            "Straight-line distance (metres) and identity of the nearest petrol/diesel fuel station " +
            "to a coordinate. Search radius is 15 km. Use for refuel-logistics reasoning and theft probability.",
            [
                new McpCapabilityParameter("lat", "number", "Latitude in decimal degrees.", IsRequired: true),
                new McpCapabilityParameter("lon", "number", "Longitude in decimal degrees.", IsRequired: true),
            ]),
        new McpCapability(
            "classify_region",
            "Classify a coordinate as urban / suburban / rural / remote based on building, road, and " +
            "amenity density in a 1 km radius. Returns an accessibility score 0-100.",
            [
                new McpCapabilityParameter("lat", "number", "Latitude in decimal degrees.", IsRequired: true),
                new McpCapabilityParameter("lon", "number", "Longitude in decimal degrees.", IsRequired: true),
            ]),
        new McpCapability(
            "calculate_route_distance",
            "Straight-line (haversine) distance in metres between two coordinates. Used as a proxy " +
            "for dispatch reach when full road-network routing isn't required.",
            [
                new McpCapabilityParameter("origin_lat", "number", "Origin latitude.", IsRequired: true),
                new McpCapabilityParameter("origin_lon", "number", "Origin longitude.", IsRequired: true),
                new McpCapabilityParameter("destination_lat", "number", "Destination latitude.", IsRequired: true),
                new McpCapabilityParameter("destination_lon", "number", "Destination longitude.", IsRequired: true),
            ]),
        new McpCapability(
            "reverse_geocode",
            "Resolve a coordinate to a postal-style address (country, state, city, road).",
            [
                new McpCapabilityParameter("lat", "number", "Latitude in decimal degrees.", IsRequired: true),
                new McpCapabilityParameter("lon", "number", "Longitude in decimal degrees.", IsRequired: true),
            ]),
        new McpCapability(
            "get_site_geocontext",
            "Composite geo-context for a site by code: coordinates, place, region classification, " +
            "and nearest fuel station. Computed once per TTL and cached — call this from alerts, " +
            "optimization, and Copilot reasoning instead of issuing the four primitive calls separately.",
            [new McpCapabilityParameter("site_code", "string", "Tower / site code, e.g. 'TWR-LEK-003'.", IsRequired: true)]),
        new McpCapability(
            "get_site_nearby",
            "Nearby infrastructure for a site by code (resolves the coordinates first).",
            [
                new McpCapabilityParameter("site_code", "string", "Tower / site code.", IsRequired: true),
                new McpCapabilityParameter("radius_metres", "integer", "Search radius in metres (default 2000).", IsRequired: false),
            ]),
    ];

    public async Task<McpInvocationResult> InvokeAsync(McpInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request.Capability.ToLowerInvariant())
        {
            case "get_nearby_infrastructure":
            {
                if (!TryReadCoord(request.Arguments, "lat", "lon", out double lat, out double lon, out string? err))
                    return Fail(request, err);
                int radius = ReadInt(request.Arguments, "radius_metres", 2000);
                OsmNearbyInfrastructure result = await osm.GetNearbyInfrastructureAsync(lat, lon, radius, cancellationToken);
                return Ok(request, result);
            }
            case "get_distance_to_fuel_station":
            {
                if (!TryReadCoord(request.Arguments, "lat", "lon", out double lat, out double lon, out string? err))
                    return Fail(request, err);
                OsmFuelStationDistance result = await osm.GetDistanceToFuelStationAsync(lat, lon, cancellationToken);
                return Ok(request, result);
            }
            case "classify_region":
            {
                if (!TryReadCoord(request.Arguments, "lat", "lon", out double lat, out double lon, out string? err))
                    return Fail(request, err);
                OsmRegionClassification result = await osm.ClassifyRegionAsync(lat, lon, cancellationToken);
                return Ok(request, result);
            }
            case "calculate_route_distance":
            {
                if (!TryReadCoord(request.Arguments, "origin_lat", "origin_lon", out double oLat, out double oLon, out string? err1))
                    return Fail(request, err1);
                if (!TryReadCoord(request.Arguments, "destination_lat", "destination_lon", out double dLat, out double dLon, out string? err2))
                    return Fail(request, err2);
                OsmRouteDistance result = await osm.CalculateRouteDistanceAsync(oLat, oLon, dLat, dLon, cancellationToken);
                return Ok(request, result);
            }
            case "reverse_geocode":
            {
                if (!TryReadCoord(request.Arguments, "lat", "lon", out double lat, out double lon, out string? err))
                    return Fail(request, err);
                OsmPlace? place = await osm.ReverseGeocodeAsync(lat, lon, cancellationToken);
                return place is null
                    ? Fail(request, "No place data returned for the supplied coordinates.")
                    : Ok(request, place);
            }
            case "get_site_geocontext":
            {
                string code = ReadString(request.Arguments, "site_code");
                if (string.IsNullOrWhiteSpace(code)) return Fail(request, "site_code is required.");
                SiteGeoContext? ctx = await geo.GetAsync(code, cancellationToken);
                return ctx is null
                    ? Fail(request, $"Site '{code}' not found or has no coordinates on file.")
                    : Ok(request, ctx);
            }
            case "get_site_nearby":
            {
                string code = ReadString(request.Arguments, "site_code");
                if (string.IsNullOrWhiteSpace(code)) return Fail(request, "site_code is required.");
                (double Lat, double Lon)? coords = await geo.GetCoordinatesAsync(code, cancellationToken);
                if (coords is null) return Fail(request, $"Site '{code}' has no coordinates on file.");
                int radius = ReadInt(request.Arguments, "radius_metres", 2000);
                OsmNearbyInfrastructure result = await osm.GetNearbyInfrastructureAsync(coords.Value.Lat, coords.Value.Lon, radius, cancellationToken);
                return Ok(request, result);
            }
            default:
                return Fail(request, $"Unknown capability '{request.Capability}'.");
        }
    }

    private static McpInvocationResult Ok(McpInvocationRequest request, object output) =>
        new(request.PluginId, request.Capability, IsSuccess: true,
            Output: output, Error: null, DurationMs: 0, CorrelationId: request.CorrelationId);

    private static McpInvocationResult Fail(McpInvocationRequest request, string? error) =>
        new(request.PluginId, request.Capability, IsSuccess: false,
            Output: null, Error: error ?? "Unknown error.", DurationMs: 0, CorrelationId: request.CorrelationId);

    private static bool TryReadCoord(
        IReadOnlyDictionary<string, object?> args,
        string latKey, string lonKey,
        out double lat, out double lon, out string? error)
    {
        if (!TryReadDouble(args, latKey, out lat) || lat is < -90 or > 90)
        {
            lon = 0; error = $"{latKey} must be a number between -90 and 90.";
            return false;
        }
        if (!TryReadDouble(args, lonKey, out lon) || lon is < -180 or > 180)
        {
            error = $"{lonKey} must be a number between -180 and 180.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryReadDouble(IReadOnlyDictionary<string, object?> args, string key, out double value)
    {
        value = 0;
        if (!args.TryGetValue(key, out object? raw) || raw is null) return false;

        switch (raw)
        {
            case double d: value = d; return true;
            case float f: value = f; return true;
            case int i: value = i; return true;
            case long l: value = l; return true;
            case string s:
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            case System.Text.Json.JsonElement je:
                if (je.ValueKind == System.Text.Json.JsonValueKind.Number)
                    return je.TryGetDouble(out value);
                if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                    return double.TryParse(je.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                return false;
            default:
                return false;
        }
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> args, string key, int fallback)
    {
        if (!args.TryGetValue(key, out object? value) || value is null) return fallback;
        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int n) => n,
            _ => fallback,
        };
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out object? value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
}
