using Microsoft.Extensions.Logging;
using Modules.Energy.Domain;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using SharedKernel;

namespace Modules.Energy.Infrastructure.Pipeline;

/// <summary>
/// Cross-module adapter: implements Network's <see cref="IEnergySyncExecutor"/> port so an OSS
/// snapshot can synchronise a site's energy plant. Mirrors <c>AlertActionExecutor</c> in the Alerts
/// module — the port is declared where it is consumed, implemented where the aggregate lives.
///
/// Two things happen per site, and the second is the one that matters most:
///   1. The <see cref="Site"/> aggregate is created or updated with the reported plant state.
///   2. A <see cref="SiteEnergyLog"/> row is appended.
///
/// That log is the append-only telemetry the existing diesel trace, OPEX projection and energy
/// trend charts already read. Writing snapshot data into it — instead of standing up a parallel
/// telemetry store — is what makes an upload show up in those views with no new plumbing at all.
///
/// It commits its own unit of work. Every module declares its own <c>IUnitOfWork</c> bound to its
/// own DbContext, so Stage 4's SaveChanges commits NetworkDbContext and nothing else — an energy
/// write left uncommitted here would be silently discarded. This mirrors the alerts path, where
/// <c>CreateOrUpdateAlertCommandHandler</c> likewise saves AlertsDbContext itself.
///
/// The consequence, stated plainly: a run is not atomic across modules. If the energy write
/// succeeds and a later module fails, the energy state stays written. That is the pre-existing
/// shape of the pipeline, not something introduced here — making it atomic would need a distributed
/// transaction or an outbox across all six contexts.
/// </summary>
internal sealed class EnergySyncExecutor(
    ISiteRepository sites,
    ISiteEnergyLogRepository logs,
    IUnitOfWork unitOfWork,
    ILogger<EnergySyncExecutor> logger) : IEnergySyncExecutor
{
    public async Task<Result<EnergySyncResult>> ExecuteAsync(
        IReadOnlyList<EnergySyncRequest> requests,
        CancellationToken cancellationToken = default)
    {
        int created = 0;
        int updated = 0;
        var telemetry = new List<SiteEnergyLog>(requests.Count);

        foreach (EnergySyncRequest request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PowerSource source = PowerSourceExtensions.FromWire(request.SourceWire);
            Site? site = await sites.GetByCodeAsync(request.SiteCode, cancellationToken);

            if (site is null)
            {
                // A site the energy module has never seen. The snapshot is the operator's own record
                // of a site they own, so it may create one — the same authority that lets it create
                // a tower.
                site = Site.CreateFromSnapshot(
                    code: request.SiteCode,
                    name: request.Name,
                    region: request.Region,
                    source: source,
                    battPct: request.BatteryPct ?? 0,
                    dieselPct: request.DieselPct ?? 0,
                    gridUp: request.GridUp,

                    // The snapshot reports no solar array. Absence of evidence isn't evidence of
                    // absence, but claiming a site has solar when the feed never said so would
                    // corrupt the fleet energy-mix figures, so the conservative reading wins.
                    hasSolar: false,
                    anomalyNote: request.AnomalyNote);

                await sites.AddAsync(site, cancellationToken);
                created++;

                logger.LogInformation(
                    "Snapshot created energy site {SiteCode} ({Region}) on {Source}",
                    request.SiteCode, request.Region, request.SourceWire);
            }
            else
            {
                // Fall back to the site's current reading for anything the snapshot didn't carry —
                // a missing battery voltage must not be read as a flat battery.
                bool changed = site.ApplyReportedState(
                    battPct: request.BatteryPct ?? site.BattPct,
                    dieselPct: request.DieselPct ?? site.DieselPct,
                    gridUp: request.GridUp,
                    source: source,
                    hasOpenAnomaly: request.HasOpenAlarm,
                    anomalyNote: request.AnomalyNote);

                if (changed)
                {
                    updated++;
                }
            }

            telemetry.Add(SiteEnergyLog.Reported(
                siteCode: site.Code,
                recordedAtUtc: request.ObservedAtUtc,
                battPct: site.BattPct,
                dieselPct: site.DieselPct,
                solarKw: site.SolarKw,
                gridUp: site.GridUp,
                activeSourceCode: (int)site.Source,

                // Cost is a rate the ticker accrues over an interval it controls. A snapshot is an
                // instant, not an interval, so it contributes no cost delta — attributing one here
                // would double-count against the ticker's own accrual.
                costNgnDelta: 0));
        }

        if (telemetry.Count > 0)
        {
            await logs.AddRangeAsync(telemetry, cancellationToken);
        }

        // Commits EnergyDbContext. Stage 4's unit of work is Network's and would not save any of this.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new EnergySyncResult(created, updated, telemetry.Count));
    }
}
