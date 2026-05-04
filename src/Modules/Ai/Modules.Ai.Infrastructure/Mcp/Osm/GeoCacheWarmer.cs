using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Network.Api;

namespace Modules.Ai.Infrastructure.Mcp.Osm;

/// <summary>
/// Background warmer that pre-fills the OSM geo cache for every known tower at
/// startup. Without this, the first /api/alerts | /api/energy/sites | /api/energy/anomalies
/// request after a fresh deploy has to fan out to public Overpass / Nominatim and
/// routinely blows the per-batch budget (Overpass queues 20–30s per query under load),
/// shipping <c>geo: null</c> for every site. By warming sequentially in the background
/// the live endpoints almost always hit a populated Redis cache and respond in single-
/// digit ms, while still degrading gracefully if the warmer hasn't finished yet.
///
/// Behaviour:
///   * Waits 20s after startup so seeders + DB migrations are done before we hit Network.
///   * Iterates towers ONE AT A TIME — Overpass throttles parallel requests per IP,
///     so spraying 15 simultaneous queries actually slows the whole batch down.
///   * Uses a dedicated long timeout (90s) per site so we don't fight the live endpoint's
///     stricter <see cref="GeoEnricher"/> batch budget.
///   * Cache hits short-circuit inside <see cref="SiteGeoLookup"/>, so subsequent boots
///     finish in milliseconds without re-issuing OSM calls.
///   * Failures are per-site and logged; one slow / failed tower does not stop the loop.
/// </summary>
internal sealed class GeoCacheWarmer(
    IServiceScopeFactory scopeFactory,
    ILogger<GeoCacheWarmer> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PerSiteTimeout = TimeSpan.FromSeconds(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        INetworkApi network = scope.ServiceProvider.GetRequiredService<INetworkApi>();
        ISiteGeoLookup geo = scope.ServiceProvider.GetRequiredService<ISiteGeoLookup>();

        IReadOnlyList<TowerSnapshot> towers;
        try
        {
            towers = await network.ListTowersAsync(stoppingToken);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GeoCacheWarmer could not list towers; skipping warm pass.");
            return;
        }

        logger.LogInformation("GeoCacheWarmer starting — {Count} tower(s) to pre-warm.", towers.Count);

        int warmed = 0, skipped = 0, failed = 0;
        foreach (TowerSnapshot tower in towers)
        {
            if (stoppingToken.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(tower.Code)) { skipped++; continue; }

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(PerSiteTimeout);

            try
            {
                SiteGeoContext? ctx = await geo.GetAsync(tower.Code, cts.Token);
                if (ctx is null) { failed++; continue; }
                warmed++;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (OperationCanceledException)
            {
                logger.LogInformation("GeoCacheWarmer hit per-site timeout for {Code}; will retry on next deploy.", tower.Code);
                failed++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "GeoCacheWarmer failed for {Code}; continuing.", tower.Code);
                failed++;
            }
        }

        logger.LogInformation(
            "GeoCacheWarmer done — warmed={Warmed} skipped={Skipped} failed={Failed} of {Total}.",
            warmed, skipped, failed, towers.Count);
    }
}
