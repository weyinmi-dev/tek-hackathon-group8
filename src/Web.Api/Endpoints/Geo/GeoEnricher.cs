using Microsoft.Extensions.Logging;
using Modules.Ai.Infrastructure.Mcp.Osm;

namespace Web.Api.Endpoints.Geo;

/// <summary>
/// Endpoint-side helper that fans out OSM lookups for a batch of site/tower codes
/// and returns a code → <see cref="GeoSummary"/> dictionary. Endpoints call this
/// after their query handler returns, then attach the resulting summaries to each
/// list item before serialization.
///
/// All work flows through <see cref="ISiteGeoLookup"/> → <c>CachedOsmClient</c> →
/// Redis, so warm-cache batches resolve in single-digit milliseconds. Cold caches
/// trigger one Overpass call per distinct site and are saved for 24h, satisfying
/// the directive's "compute once, reuse" rule.
///
/// **Resilience contract**: enrichment is strictly additive. Any failure — OSM
/// unreachable, Redis down, tower lookup throwing, request canceled — is caught
/// per-item, logged, and turned into a missing entry in the result map. The
/// caller will see <c>geo == null</c> for that site rather than the endpoint
/// returning a 500. This is critical: the dashboard / alerts / energy pages
/// fetch alerts in parallel with metrics &amp; map, and a failure here used to
/// fail-fast the whole <c>Promise.all</c> on the frontend, leaving the page
/// blank. Geo is decoration; it must never block the core response.
/// </summary>
public sealed class GeoEnricher(ISiteGeoLookup geoLookup, ILogger<GeoEnricher> logger)
{
    /// <summary>
    /// Cache-only variant used by the batch path. A miss is a null, not an OSM call — see
    /// <see cref="ForSitesAsync"/> for why that distinction is the whole point.
    /// </summary>
    private async Task<GeoSummary?> ForCachedSiteAsync(string? siteCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return null;
        try
        {
            SiteGeoContext? ctx = await geoLookup.GetCachedAsync(siteCode, cancellationToken);
            return ctx is null ? null : Map(ctx);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A cache outage must not take the page with it. Geo is decoration.
            logger.LogWarning(ex, "Cached geo read failed for site {SiteCode}; returning null geo.", siteCode);
            return null;
        }
    }

    /// <summary>
    /// Full lookup, including live OSM calls on a cache miss. Only for the single-site endpoint,
    /// where an operator has explicitly asked for one site's geo context and is waiting on it.
    /// Never call this in a loop over a list — that is what <see cref="ForSitesAsync"/> is for.
    /// </summary>
    public async Task<GeoSummary?> ForSiteAsync(string? siteCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return null;
        try
        {
            SiteGeoContext? ctx = await geoLookup.GetAsync(siteCode, cancellationToken);
            return ctx is null ? null : Map(ctx);
        }
        catch (OperationCanceledException)
        {
            // Caller bailed (request aborted, shutdown). Don't log — this is normal.
            throw;
        }
        catch (Exception ex)
        {
            // Anything else: cache outage, OSM error, db hiccup — log and degrade.
            logger.LogWarning(ex, "Geo enrichment failed for site {SiteCode}; returning null geo.", siteCode);
            return null;
        }
    }

    /// <summary>
    /// Wall-clock ceiling for a batch. It is small because a batch now only reads Redis — see
    /// <see cref="ForSitesAsync"/>. Two seconds is a cache outage, not a slow lookup.
    ///
    /// It used to be 30 seconds, to accommodate live Overpass calls on a cache miss. That was the
    /// bug: a list endpoint would sit on the full budget whenever OSM was slow or unreachable, so
    /// /api/alerts and /api/energy/sites took 30 seconds to return rows the database had produced in
    /// milliseconds — and because a failed lookup was never cached, every refresh paid it again.
    /// </summary>
    private static readonly TimeSpan BatchBudget = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Resolve a batch of distinct site codes from the geo CACHE ONLY. A site that has not been
    /// warmed yet simply comes back without geo; nothing here goes near the network.
    ///
    /// This is the difference between decoration and a stall. The class contract has always said geo
    /// "must never block the core response", but the batch used to call
    /// <see cref="ISiteGeoLookup.GetAsync"/>, which issues three sequential OSM queries on a cache
    /// miss. Every site the operator created that the startup warmer didn't know about — every site
    /// arriving from an OSS snapshot, for instance — was a guaranteed miss, and the alerts and energy
    /// pages spent the full batch budget waiting on an OSM that could not answer.
    ///
    /// The cache is filled out of band by <c>GeoCacheWarmer</c>, and by the per-site endpoint
    /// (<c>GET /geo/site/{code}</c>) when an operator selects a site on the map and is willing to
    /// wait for the answer. Both are off the page-load path, which is where they belong.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, GeoSummary>> ForSitesAsync(
        IEnumerable<string?> siteCodes,
        CancellationToken cancellationToken)
    {
        string[] distinct = siteCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length == 0)
        {
            return new Dictionary<string, GeoSummary>(StringComparer.OrdinalIgnoreCase);
        }

        // Linked CTS: the request CT (client disconnect / shutdown) PLUS our batch
        // budget timeout. Either firing cancels every in-flight OSM HTTP call so the
        // HttpClient inside OsmClient releases the socket immediately.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(BatchBudget);

        Task<(string Code, GeoSummary? Geo)>[] tasks = distinct
            .Select(async code =>
            {
                try
                {
                    GeoSummary? geo = await ForCachedSiteAsync(code, cts.Token);
                    return (code, geo);
                }
                catch (OperationCanceledException)
                {
                    // Budget expired (or the parent CT fired). Treat as missing geo —
                    // the underlying request itself isn't necessarily over; only the
                    // geo fan-out is bounded.
                    return (code, (GeoSummary?)null);
                }
            })
            .ToArray();

        (string Code, GeoSummary? Geo)[] results;
        try
        {
            results = await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller is gone. Propagate so ASP.NET stops the pipeline.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Our budget expired. Reap whatever individual tasks did finish; the
            // rest are abandoned with a null entry below.
            logger.LogInformation(
                "Geo batch hit the {Budget}s budget for {Count} site(s); returning partial map.",
                BatchBudget.TotalSeconds, distinct.Length);
            results = tasks.Select(t => t.IsCompletedSuccessfully ? t.Result : ("", (GeoSummary?)null)).ToArray();
        }
        catch (Exception ex)
        {
            // Defence-in-depth: anything truly unexpected (DI / cache / framework bug)
            // returns an empty map rather than 500ing the endpoint.
            logger.LogWarning(ex, "Batch geo enrichment threw; returning partial map.");
            return new Dictionary<string, GeoSummary>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, GeoSummary> map = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string code, GeoSummary? geo) in results)
        {
            if (!string.IsNullOrEmpty(code) && geo is not null) map[code] = geo;
        }
        return map;
    }

    private static GeoSummary Map(SiteGeoContext ctx) => new(
        Latitude: ctx.Coordinates.Latitude,
        Longitude: ctx.Coordinates.Longitude,
        RegionType: ctx.Classification.RegionType,
        AccessibilityScore: ctx.Classification.AccessibilityScore,
        NearestFuelStationMetres: ctx.NearestFuelStation.Found && ctx.NearestFuelStation.StraightLineMetres.HasValue
            ? (int)Math.Round(ctx.NearestFuelStation.StraightLineMetres.Value)
            : null,
        NearestFuelStationName: ctx.NearestFuelStation.Name,
        Address: ctx.Place?.DisplayName);
}
