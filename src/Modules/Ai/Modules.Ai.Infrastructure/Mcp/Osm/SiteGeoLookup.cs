using Application.Abstractions.Caching;
using Microsoft.Extensions.Options;
using Modules.Ai.Infrastructure.Configuration;
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
    /// <summary>
    /// Full lookup: reads the cache, and on a miss calls OSM (three sequential queries) and caches
    /// the result. May take tens of seconds when OSM is slow or unreachable, so it belongs only on
    /// paths where the user has explicitly asked for one site's geo and is prepared to wait.
    /// </summary>
    Task<SiteGeoContext?> GetAsync(string siteCode, CancellationToken ct = default);

    /// <summary>
    /// Cache-only lookup. Returns null on a miss and never touches the network.
    ///
    /// This exists because geo is decoration on list endpoints (alerts, energy sites, anomalies) and
    /// decoration must not be able to stall the page. <see cref="GetAsync"/> on a cold site issues
    /// live OSM calls; with N sites on a list and OSM unreachable, the endpoint sat on its timeout
    /// budget and took 30 seconds to return data the database had produced in milliseconds.
    /// List endpoints use this instead: geo appears once the cache is warm, and is simply absent
    /// until then.
    /// </summary>
    Task<SiteGeoContext?> GetCachedAsync(string siteCode, CancellationToken ct = default);

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

    public async Task<SiteGeoContext?> GetCachedAsync(string siteCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return null;

        return await _cache.GetAsync<SiteGeoContext>(CtxKeyPrefix + siteCode.ToUpperInvariant(), ct);
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
