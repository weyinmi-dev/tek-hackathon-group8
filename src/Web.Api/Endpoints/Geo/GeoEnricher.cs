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
    /// Maximum wall-clock budget for an entire geo enrichment batch. Public Overpass
    /// queues per-IP and routinely takes 20–30s per query under load, so the original
    /// 8s ceiling killed every cold-cache batch outright and Redis never warmed —
    /// /api/alerts shipped <c>geo: null</c> on every request even for valid towers.
    /// 30s is the realistic ceiling: long enough that two sequential Overpass calls
    /// per site in parallel can finish on a slow day, still well under Next.js dev
    /// rewrite + Aspire DCP socket limits. Once Redis is warm (24h TTL) requests
    /// resolve in single-digit ms regardless. The startup warmer (GeoCacheWarmer)
    /// pre-fills the cache out of band so users don't pay this cost on the first
    /// page load after a deploy.
    /// </summary>
    private static readonly TimeSpan BatchBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Resolve a batch of distinct site codes in parallel. Duplicates collapse before
    /// dispatch so we never issue two OSM calls for the same code in one request.
    /// Per-item failures degrade silently to a missing dictionary entry (see class doc).
    /// The whole batch is bounded by <see cref="BatchBudget"/> — late stragglers are
    /// abandoned so the endpoint never waits longer than the proxy will tolerate.
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
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(BatchBudget);

        Task<(string Code, GeoSummary? Geo)>[] tasks = distinct
            .Select(async code =>
            {
                try
                {
                    GeoSummary? geo = await ForSiteAsync(code, cts.Token);
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
