using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Telemetry;

/// <summary>
/// Background loop that mutates Site state every <see cref="TickInterval"/> seconds and
/// records a <see cref="SiteEnergyLog"/> snapshot per site. This is what gives the
/// dashboards a "live" feel — battery drains while running on battery, diesel burns when
/// on generator, solar charges the battery, and the trace charts tick forward in real time.
///
/// Cadence is intentionally aggressive (30s) for the demo. In production this would either
/// be longer (5m+) or run as an external job consuming real telemetry from MQTT/IoT Hub.
/// </summary>
internal sealed class EnergyTickerService(
    IServiceScopeFactory scopeFactory,
    ILogger<EnergyTickerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so seeding has finished and the first tick observes a populated db.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        int tickSeed = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickOnceAsync(tickSeed++, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Log + continue; one bad tick must not kill the whole loop. The next tick
                // will retry against a clean DbContext scope.
                logger.LogError(ex, "Energy ticker pass failed; will retry next interval.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task TickOnceAsync(int seed, CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        EnergyDbContext db = scope.ServiceProvider.GetRequiredService<EnergyDbContext>();

        List<Site> sites = await db.Sites.ToListAsync(ct);
        if (sites.Count == 0)
        {
            return;
        }

        var openAnomaliesBySite = (await db.Anomalies
                .AsNoTracking()
                .Where(a => !a.Acknowledged)
                .Select(a => a.SiteCode)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshots = new List<SiteEnergyLog>(sites.Count);
        foreach (Site site in sites)
        {
            site.ApplyTick(seed + Math.Abs(site.Code.GetHashCode() % 17),
                hasOpenAnomaly: openAnomaliesBySite.Contains(site.Code));

            snapshots.Add(SiteEnergyLog.Snapshot(
                site.Code,
                site.BattPct, site.DieselPct, site.SolarKw, site.GridUp,
                (int)site.Source,
                costNgnDelta: site.DailyCostNgn / 2880));
        }

        await db.SiteLogs.AddRangeAsync(snapshots, ct);
        await db.SaveChangesAsync(ct);

        logger.LogDebug("Energy tick {Seed} updated {Count} sites.", seed, sites.Count);
    }
}
