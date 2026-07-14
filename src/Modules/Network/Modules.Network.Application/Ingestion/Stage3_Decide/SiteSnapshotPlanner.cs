using Application.Abstractions.Pipeline;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Turns reported site snapshots into the pipeline actions that synchronise every affected
/// aggregate. The snapshot counterpart of <see cref="DefaultDecisionEngine"/>, and held to the same
/// contract: no DbContext, no clock, no logger, no I/O. Given the same snapshot and the same
/// current state it always plans the same actions, which is what makes synchronisation testable and
/// idempotent rather than merely "usually fine".
///
/// The one authority difference from the AI engine is deliberate. <see cref="DefaultDecisionEngine"/>
/// refuses to create towers — an analyzer hallucinating a site would corrupt the topology. A
/// snapshot is the operator's own system of record for a site it owns, so it *may* create one. That
/// asymmetry is the reason snapshot planning lives here rather than being folded into the AI engine.
/// </summary>
public sealed class SiteSnapshotPlanner : ISiteSnapshotPlanner
{
    /// <summary>
    /// Prefix that scopes a provider's alarm id into our fingerprint space. Alarm ids are unique
    /// within a provider's OSS but carry no guarantee against colliding with an AI fingerprint, and
    /// the two must never dedupe into each other — an inferred anomaly and a reported alarm are
    /// different claims about the world.
    /// </summary>
    public const string AlarmFingerprintPrefix = "OSS:";

    /// <summary>An alarm is a stated fact, not an inference, so it carries no uncertainty.</summary>
    private const double ReportedFactConfidence = 1.0;

    public IReadOnlyList<PipelineAction> Plan(
        IReadOnlyList<SiteSnapshotPayload> snapshots,
        IReadOnlyList<AlertSnapshot> activeAlerts,
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(activeAlerts);
        ArgumentNullException.ThrowIfNull(currentTowers);

        if (snapshots.Count == 0)
        {
            return [];
        }

        var actions = new List<PipelineAction>();

        // Live alerts that came from an OSS alarm, indexed by fingerprint. Only these are eligible
        // to be resolved by an absent alarm — an alert our analyzer inferred is not the provider's
        // to close, and clearing it because a snapshot didn't mention it would silently discard our
        // own detections.
        Dictionary<string, AlertSnapshot> liveAlarmAlerts = activeAlerts
            .Where(a => !a.IsResolved && a.AnomalyFingerprint.StartsWith(AlarmFingerprintPrefix, StringComparison.Ordinal))
            .GroupBy(a => a.AnomalyFingerprint, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Fingerprints still being reported across every site in this upload. Anything live and
        // absent from this set has cleared. Gathered across all snapshots before any resolve is
        // planned, so a batch covering many sites cannot resolve a site's alarms using another
        // site's document.
        var stillReported = new HashSet<string>(StringComparer.Ordinal);
        var sitesInThisUpload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SiteSnapshotPayload snapshot in snapshots)
        {
            sitesInThisUpload.Add(snapshot.Site.SiteCode);
            PlanForSite(snapshot, liveAlarmAlerts, currentTowers, actions, stillReported);
        }

        // ── Alarm clearance ─────────────────────────────────────────────────────
        // Scoped to the sites this upload actually covered. A snapshot for Lagos says nothing
        // about whether Abuja's alarms are still up, so it must not close them.
        foreach ((string fingerprint, AlertSnapshot alert) in liveAlarmAlerts)
        {
            if (stillReported.Contains(fingerprint) || !sitesInThisUpload.Contains(alert.TowerCode))
            {
                continue;
            }

            actions.Add(new ResolveAlarmAction(
                fingerprint,
                Reason: "Alarm no longer reported by the provider's latest site snapshot."));
        }

        return actions;
    }

    private static void PlanForSite(
        SiteSnapshotPayload snapshot,
        IReadOnlyDictionary<string, AlertSnapshot> liveAlarmAlerts,
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers,
        List<PipelineAction> actions,
        HashSet<string> stillReported)
    {
        SnapshotSite site = snapshot.Site;
        SnapshotPerformanceMetrics? performance = snapshot.Performance;
        SnapshotEnvironmentalMetrics? environment = snapshot.Environmental;

        // The observation time is the snapshot's own, never "now". Re-planning a stored snapshot
        // must produce the same actions it did the first time; stamping it with the wall clock
        // would make the planner non-deterministic and break replay.
        DateTime observedAt = (performance?.CapturedAt ?? snapshot.GeneratedAt).UtcDateTime;

        string statusWire = SnapshotDerivations.TowerStatusFrom(site.HealthScore, snapshot.ActiveAlarms);
        List<SnapshotAlarm> openAlarms = snapshot.ActiveAlarms.Where(SnapshotDerivations.IsOpen).ToList();

        // ── Topology ────────────────────────────────────────────────────────────
        actions.Add(new UpsertTowerAction(
            TowerCode: site.SiteCode,
            Name: site.SiteName,
            Region: site.Region,
            Latitude: site.Latitude,
            Longitude: site.Longitude,
            SignalPct: SnapshotDerivations.SignalPctFromRsrp(SnapshotDerivations.Kpi(performance?.Kpis, "RSRP")),
            LoadPct: performance?.CellUtilizationPercent,
            StatusWire: statusWire,
            Issue: DescribeIssue(openAlarms)));

        // ── Alarms → alerts ─────────────────────────────────────────────────────
        // Routed through the same create-vs-update decision the AI path uses, so an alarm that
        // keeps being reported bumps its occurrence count instead of spawning a duplicate alert.
        foreach (SnapshotAlarm alarm in openAlarms)
        {
            string fingerprint = AlarmFingerprint(alarm.AlarmId);
            stillReported.Add(fingerprint);

            liveAlarmAlerts.TryGetValue(fingerprint, out AlertSnapshot? existing);

            actions.Add(new SyncAlarmAction(
                AnomalyFingerprint: fingerprint,
                ExistingAlertId: existing?.Id,
                SeverityWire: MapSeverity(alarm.Severity),
                TowerCode: site.SiteCode,
                Region: site.Region,
                Title: BuildAlarmTitle(alarm, site.SiteCode),
                Cause: alarm.Description ?? alarm.Type ?? "Alarm reported by the provider.",
                RaisedAtUtc: (alarm.RaisedAt ?? snapshot.GeneratedAt).UtcDateTime));
        }

        // ── Equipment ───────────────────────────────────────────────────────────
        if (site.Equipment.Count > 0)
        {
            actions.Add(new SyncEquipmentAction(
                SiteCode: site.SiteCode,
                Reported: site.Equipment
                    .Select(e => new EquipmentReport(e.EquipmentId, e.Type, e.Model, e.Status))
                    .ToList(),
                ObservedAtUtc: observedAt));
        }

        // ── Maintenance ─────────────────────────────────────────────────────────
        if (snapshot.Maintenance is { } maintenance)
        {
            actions.Add(new SyncMaintenanceAction(
                SiteCode: site.SiteCode,
                OpenTickets: maintenance.OpenTickets
                    .Select(t => new TicketReport(
                        t.TicketId,
                        t.Priority,
                        t.Status,
                        t.Issue,
                        t.AssignedEngineer?.EngineerId,
                        t.AssignedEngineer?.Name,
                        t.CreatedAt,
                        t.EstimatedArrival))
                    .ToList(),
                CompletedWork: maintenance.MaintenanceHistory
                    .Select(h => new CompletedWorkReport(h.TicketId, h.CompletedAt, h.Engineer, h.Action))
                    .ToList(),
                ObservedAtUtc: observedAt));
        }

        // ── Energy ──────────────────────────────────────────────────────────────
        // Only planned when the snapshot actually carried plant readings. Synthesising a site with
        // 0% battery and 0% fuel from a missing block would derive it as Critical and raise alarm
        // on a site nobody reported a problem with.
        if (environment is not null)
        {
            int? battery = SnapshotDerivations.BatteryPctFromVoltage(environment.BatteryVoltage);
            int? diesel = environment.GeneratorFuelPercent;

            if (battery is not null || diesel is not null)
            {
                actions.Add(new SyncEnergySiteAction(
                    SiteCode: site.SiteCode,
                    Name: site.SiteName,
                    Region: site.Region,
                    BatteryPct: battery,
                    DieselPct: diesel,
                    GridUp: environment.MainPowerAvailable ?? true,
                    SourceWire: DerivePowerSource(environment),
                    HasOpenAlarm: openAlarms.Count > 0,
                    AnomalyNote: DescribeIssue(openAlarms),
                    ObservedAtUtc: observedAt));
            }
        }
    }

    public static string AlarmFingerprint(string alarmId) => $"{AlarmFingerprintPrefix}{alarmId.Trim()}";

    /// <summary>
    /// Which source is actually carrying the load, from what the plant reports. Grid presence wins;
    /// otherwise a running generator; otherwise the site is coasting on batteries.
    /// </summary>
    private static string DerivePowerSource(SnapshotEnvironmentalMetrics environment)
    {
        if (environment.MainPowerAvailable == true)
        {
            return "grid";
        }

        return environment.GeneratorRunning == true ? "generator" : "battery";
    }

    /// <summary>
    /// Maps the provider's alarm severity onto ours. Ours has three levels, theirs has more, so
    /// Major and Minor both land on Warn — collapsing them loses granularity but inventing a fourth
    /// level to preserve it would change the meaning of every existing alert in the system.
    /// </summary>
    private static string MapSeverity(string? severity) => severity?.Trim().ToUpperInvariant() switch
    {
        "CRITICAL" => "CRITICAL",
        "MAJOR" or "MINOR" or "WARNING" => "WARN",
        _ => "INFO"
    };

    private static string BuildAlarmTitle(SnapshotAlarm alarm, string siteCode)
    {
        string what = alarm.Type ?? alarm.Category ?? "Alarm";
        return $"{what} on {siteCode}";
    }

    /// <summary>The most severe open alarm, used as the human-readable issue line on a tower/site.</summary>
    private static string? DescribeIssue(IReadOnlyList<SnapshotAlarm> openAlarms)
    {
        if (openAlarms.Count == 0)
        {
            return null;
        }

        SnapshotAlarm worst = openAlarms
            .OrderByDescending(a => SeverityRank(a.Severity))
            .ThenBy(a => a.AlarmId, StringComparer.Ordinal)
            .First();

        return worst.Description ?? worst.Type ?? worst.Category;
    }

    private static int SeverityRank(string? severity) => severity?.Trim().ToUpperInvariant() switch
    {
        "CRITICAL" => 3,
        "MAJOR" => 2,
        "MINOR" or "WARNING" => 1,
        _ => 0
    };
}
