using Application.Abstractions.Caching;
using Microsoft.Extensions.Options;
using Modules.Ai.Infrastructure.SemanticKernel;
using Modules.Network.Api;

namespace Modules.Ai.Infrastructure.Mcp.Osm;

/// <summary>
/// Resolves a site / tower code to its geographic context. Energy <c>Site</c> records don't
/// carry coordinates yet, but the matching <c>Tower</c> in the Network module does — they're
/// joined 1:1 by Code. We pull the tower snapshot through <see cref="INetworkApi"/> (in-process,
/// respects the modular-monolith boundary) and then enrich with the OSM-derived attributes the
/// directive asks us to compute once and reuse: region type, accessibility score, nearest fuel
/// station distance.
///
/// The composite <see cref="SiteGeoContext"/> is cached for the configured TTL, so alerts,
/// optimization, and the Copilot all see the same answer without re-hitting OSM.
/// </summary>
public interface ISiteGeoLookup
{
    Task<SiteGeoContext?> GetAsync(string siteCode, CancellationToken ct = default);
    Task<(double Lat, double Lon)?> GetCoordinatesAsync(string siteCode, CancellationToken ct = default);
}

internal sealed class SiteGeoLookup : ISiteGeoLookup
{
    private const string CtxKeyPrefix = "osm:site-ctx:";
    private const string CoordKeyPrefix = "osm:site-coord:";

    private readonly INetworkApi _network;
    private readonly IOsmClient _osm;
    private readonly ICacheService _cache;
    private readonly TimeSpan _ttl;

    public SiteGeoLookup(
        INetworkApi network,
        IOsmClient osm,
        ICacheService cache,
        IOptions<AiOptions> ai)
    {
        _network = network;
        _osm = osm;
        _cache = cache;
        _ttl = TimeSpan.FromHours(Math.Max(1, ai.Value.Osm.CacheHours));
    }

    public async Task<(double Lat, double Lon)?> GetCoordinatesAsync(string siteCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return null;

        string key = CoordKeyPrefix + siteCode.ToUpperInvariant();
        CoordCacheRecord? cached = await _cache.GetAsync<CoordCacheRecord>(key, ct);
        if (cached is not null) return (cached.Lat, cached.Lon);

        TowerSnapshot? tower = await _network.GetByCodeAsync(siteCode, ct);
        if (tower is null || (tower.Latitude == 0 && tower.Longitude == 0)) return null;

        await _cache.SetAsync(key, new CoordCacheRecord(tower.Latitude, tower.Longitude), _ttl, ct);
        return (tower.Latitude, tower.Longitude);
    }

    public async Task<SiteGeoContext?> GetAsync(string siteCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return null;

        string key = CtxKeyPrefix + siteCode.ToUpperInvariant();
        SiteGeoContext? cached = await _cache.GetAsync<SiteGeoContext>(key, ct);
        if (cached is not null) return cached;

        (double Lat, double Lon)? coords = await GetCoordinatesAsync(siteCode, ct);
        if (coords is null) return null;

        // Issue OSM calls sequentially — Overpass dislikes parallel bursts from the same client.
        OsmRegionClassification classification = await _osm.ClassifyRegionAsync(coords.Value.Lat, coords.Value.Lon, ct);
        OsmFuelStationDistance fuel = await _osm.GetDistanceToFuelStationAsync(coords.Value.Lat, coords.Value.Lon, ct);
        OsmPlace? place = await _osm.ReverseGeocodeAsync(coords.Value.Lat, coords.Value.Lon, ct);

        SiteGeoContext ctx = new(
            SiteCode: siteCode.ToUpperInvariant(),
            Coordinates: new OsmCoordinates(coords.Value.Lat, coords.Value.Lon),
            Place: place,
            Classification: classification,
            NearestFuelStation: fuel,
            ComputedAtUtc: DateTime.UtcNow);

        await _cache.SetAsync(key, ctx, _ttl, ct);
        return ctx;
    }

    private sealed record CoordCacheRecord(double Lat, double Lon);
}
