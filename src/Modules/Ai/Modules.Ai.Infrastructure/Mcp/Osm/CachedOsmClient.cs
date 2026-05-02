using System.Globalization;
using Application.Abstractions.Caching;
using Microsoft.Extensions.Options;
using Modules.Ai.Infrastructure.SemanticKernel;

namespace Modules.Ai.Infrastructure.Mcp.Osm;

/// <summary>
/// Caching decorator over <see cref="IOsmClient"/>. Every OSM call is keyed on its
/// inputs and stored in Redis with a long TTL — geographic facts change slowly and
/// the public OSM endpoints have strict per-IP rate limits, so reusing a result for
/// 24 hours is both polite and necessary. The directive requires that the system
/// "Avoid repeated MCP calls for the same data" and "Compute once → reuse across
/// Analytics, Alerts, Optimization" — Redis is the first half of that contract;
/// durable per-Site geo attributes are the second half (see <see cref="SiteGeoLookup"/>).
/// </summary>
internal sealed class CachedOsmClient : IOsmClient
{
    private const string Prefix = "osm:";

    private readonly IOsmClient _inner;
    private readonly ICacheService _cache;
    private readonly TimeSpan _ttl;

    public CachedOsmClient(IOsmClient inner, ICacheService cache, IOptions<AiOptions> ai)
    {
        _inner = inner;
        _cache = cache;
        _ttl = TimeSpan.FromHours(Math.Max(1, ai.Value.Osm.CacheHours));
    }

    public async Task<OsmPlace?> ReverseGeocodeAsync(double lat, double lon, CancellationToken ct = default)
    {
        string key = Key("reverse", Round(lat), Round(lon));
        OsmPlace? cached = await _cache.GetAsync<OsmPlace>(key, ct);
        if (cached is not null) return cached;

        OsmPlace? fresh = await _inner.ReverseGeocodeAsync(lat, lon, ct);
        if (fresh is not null) await _cache.SetAsync(key, fresh, _ttl, ct);
        return fresh;
    }

    public async Task<OsmNearbyInfrastructure> GetNearbyInfrastructureAsync(
        double lat, double lon, int radiusMetres, CancellationToken ct = default)
    {
        string key = Key("nearby", Round(lat), Round(lon), radiusMetres.ToString(CultureInfo.InvariantCulture));
        OsmNearbyInfrastructure? cached = await _cache.GetAsync<OsmNearbyInfrastructure>(key, ct);
        if (cached is not null) return cached;

        OsmNearbyInfrastructure fresh = await _inner.GetNearbyInfrastructureAsync(lat, lon, radiusMetres, ct);
        await _cache.SetAsync(key, fresh, _ttl, ct);
        return fresh;
    }

    public async Task<OsmFuelStationDistance> GetDistanceToFuelStationAsync(
        double lat, double lon, CancellationToken ct = default)
    {
        string key = Key("fuel", Round(lat), Round(lon));
        OsmFuelStationDistance? cached = await _cache.GetAsync<OsmFuelStationDistance>(key, ct);
        if (cached is not null) return cached;

        OsmFuelStationDistance fresh = await _inner.GetDistanceToFuelStationAsync(lat, lon, ct);
        await _cache.SetAsync(key, fresh, _ttl, ct);
        return fresh;
    }

    public async Task<OsmRegionClassification> ClassifyRegionAsync(
        double lat, double lon, CancellationToken ct = default)
    {
        string key = Key("region", Round(lat), Round(lon));
        OsmRegionClassification? cached = await _cache.GetAsync<OsmRegionClassification>(key, ct);
        if (cached is not null) return cached;

        OsmRegionClassification fresh = await _inner.ClassifyRegionAsync(lat, lon, ct);
        await _cache.SetAsync(key, fresh, _ttl, ct);
        return fresh;
    }

    public async Task<OsmRouteDistance> CalculateRouteDistanceAsync(
        double originLat, double originLon, double destinationLat, double destinationLon,
        CancellationToken ct = default)
    {
        string key = Key("route", Round(originLat), Round(originLon), Round(destinationLat), Round(destinationLon));
        OsmRouteDistance? cached = await _cache.GetAsync<OsmRouteDistance>(key, ct);
        if (cached is not null) return cached;

        OsmRouteDistance fresh = await _inner.CalculateRouteDistanceAsync(
            originLat, originLon, destinationLat, destinationLon, ct);
        await _cache.SetAsync(key, fresh, _ttl, ct);
        return fresh;
    }

    private static string Key(params string[] parts) =>
        Prefix + string.Join(":", parts);

    // Snap to ~11m grid so neighbouring queries reuse the same cache cell. OSM data
    // is not accurate enough to distinguish sub-decimetre coordinates anyway.
    private static string Round(double v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
}
