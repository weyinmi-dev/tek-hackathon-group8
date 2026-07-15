using Microsoft.Extensions.Logging;
using Application.Abstractions.Pipeline;
using Modules.Energy.Domain;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using Modules.Network.Domain.Ingestion;
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
    IAnomalyEventRepository anomalies,
    IUnitOfWork unitOfWork,
    ILogger<EnergySyncExecutor> logger) : IEnergySyncExecutor
{
    public async Task<Result<EnergySyncResult>> ExecuteAsync(
        IReadOnlyList<EnergySyncRequest> requests,
        CancellationToken cancellationToken = default)
    {
        int created = 0;
        int updated = 0;
        int anomaliesCreated = 0;
        int anomaliesUpdated = 0;
        int anomaliesResolved = 0;
        var telemetry = new List<SiteEnergyLog>(requests.Count);
        var changes = new List<SyncChange>();

        foreach (EnergySyncRequest request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PowerSource source = PowerSourceExtensions.FromWire(request.SourceWire);
            Site? site = await sites.GetByCodeAsync(request.SiteCode, cancellationToken);

            // Anomalies are synchronised before the site's health is recomputed, because an open
            // anomaly is one of the inputs to that derivation — deriving health first would rate a
            // site healthy on the same pass that discovered its battery is failing.
            AnomalySyncOutcome anomalyOutcome =
                await SyncAnomaliesAsync(request, changes, cancellationToken);

            anomaliesCreated += anomalyOutcome.Created;
            anomaliesUpdated += anomalyOutcome.Updated;
            anomaliesResolved += anomalyOutcome.Resolved;

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

                changes.Add(new SyncChange(
                    "Energy Site", site.Code, SyncAction.Created, site.Code,
                    $"{request.Region} · {request.SourceWire} · battery {site.BattPct}% · fuel {site.DieselPct}%"));

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

                    // An open anomaly degrades the site's health just as an open alarm does. Passing
                    // only the alarm flag would let a site with a failing battery and no alarm keep
                    // reporting itself healthy.
                    hasOpenAnomaly: request.HasOpenAlarm || anomalyOutcome.HasOpen,

                    anomalyNote: request.AnomalyNote ?? anomalyOutcome.WorstDetail);

                if (changed)
                {
                    updated++;
                    changes.Add(new SyncChange(
                        "Energy Site", site.Code, SyncAction.Updated, site.Code,
                        $"{request.SourceWire} · battery {site.BattPct}% · fuel {site.DieselPct}% · " +
                        $"grid {(site.GridUp ? "up" : "down")} · health {site.Health}"));
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

        return Result.Success(new EnergySyncResult(
            created, updated, telemetry.Count,
            anomaliesCreated, anomaliesUpdated, anomaliesResolved, changes));
    }

    /// <summary>
    /// Reconciles the anomalies a site is currently exhibiting against the ones already stored for it.
    ///
    /// Keyed on the <i>condition</i> (site + kind), not on the sighting. A site running on a failing
    /// battery re-reports that fact on every poll; keyed on the sighting, that would be a fresh
    /// anomaly row every fifteen minutes until the anomalies page was useless. So:
    ///   detected, not stored     → create
    ///   detected, already stored → refresh it (and reopen it if it had been acknowledged)
    ///   stored, no longer detected → auto-resolve
    ///
    /// Only rows this synchronisation created are touched — those carrying a DetectionKey. An anomaly
    /// the ML detector or the seeder owns is left alone; closing someone else's detection because our
    /// rules don't happen to reproduce it would be overstepping.
    /// </summary>
    private async Task<AnomalySyncOutcome> SyncAnomaliesAsync(
        EnergySyncRequest request,
        List<SyncChange> changes,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AnomalyEvent> stored =
            await anomalies.ListSnapshotDetectedForUpdateAsync(request.SiteCode, cancellationToken);

        Dictionary<string, AnomalyEvent> byKey = stored
            .Where(a => a.DetectionKey is not null)
            .ToDictionary(a => a.DetectionKey!, StringComparer.Ordinal);

        int created = 0, updated = 0, resolved = 0;
        var detectedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (DetectedEnergyAnomaly detected in request.Anomalies)
        {
            AnomalyKind kind = (AnomalyKind)(int)detected.Kind;
            AnomalySeverity severity = (AnomalySeverity)(int)detected.Severity;
            string key = AnomalyEvent.KeyFor(request.SiteCode, kind);
            detectedKeys.Add(key);

            if (byKey.TryGetValue(key, out AnomalyEvent? existing))
            {
                if (existing.Observe(severity, detected.Detail, detected.Confidence, detected.ObservedAt.UtcDateTime))
                {
                    updated++;
                    changes.Add(new SyncChange(
                        "Anomaly", kind.ToWire(), SyncAction.Updated, request.SiteCode, detected.Detail));
                }

                continue;
            }

            await anomalies.AddAsync(
                AnomalyEvent.DetectFromSnapshot(
                    siteCode: request.SiteCode,
                    kind: kind,
                    severity: severity,
                    detail: detected.Detail,
                    confidence: detected.Confidence,
                    detectedAtUtc: detected.ObservedAt.UtcDateTime,
                    detectionKey: key),
                cancellationToken);

            created++;
            changes.Add(new SyncChange(
                "Anomaly", kind.ToWire(), SyncAction.Created, request.SiteCode, detected.Detail));

            logger.LogInformation(
                "Snapshot detected {Kind} anomaly at {SiteCode}: {Detail}",
                kind, request.SiteCode, detected.Detail);
        }

        // Conditions we previously detected and no longer do. The site fixed itself, or was fixed.
        foreach (AnomalyEvent stale in stored)
        {
            if (stale.DetectionKey is null || detectedKeys.Contains(stale.DetectionKey))
            {
                continue;
            }

            if (stale.AutoResolve(request.ObservedAtUtc))
            {
                resolved++;
                changes.Add(new SyncChange(
                    "Anomaly", stale.Kind.ToWire(), SyncAction.Archived, request.SiteCode,
                    "Condition cleared — no longer reported by the latest snapshot."));

                logger.LogInformation(
                    "Anomaly {Kind} at {SiteCode} auto-resolved — condition cleared",
                    stale.Kind, request.SiteCode);
            }
        }

        // What the site's health derivation needs to know, computed from the state we just wrote
        // rather than from a second query.
        List<AnomalyEvent> open = stored
            .Where(a => a.DetectionKey is not null && detectedKeys.Contains(a.DetectionKey))
            .ToList();

        bool hasOpen = created > 0 || open.Count > 0;

        string? worst = request.Anomalies
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.Confidence)
            .FirstOrDefault()?.Detail;

        return new AnomalySyncOutcome(created, updated, resolved, hasOpen, worst);
    }

    private sealed record AnomalySyncOutcome(
        int Created, int Updated, int Resolved, bool HasOpen, string? WorstDetail);
}
