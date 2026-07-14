using Microsoft.Extensions.Logging;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Domain.Assets;
using Modules.Network.Domain.Maintenance;
using Modules.Network.Domain.Towers;

namespace Modules.Network.Application.Ingestion.Stage4_Persist;

/// <summary>
/// Applies the Network-owned half of a snapshot synchronisation: topology, equipment, maintenance
/// tickets and engineers. Alerts and energy are executed by their owning modules through
/// <see cref="IAlertActionExecutor"/> and <see cref="IEnergySyncExecutor"/>; this handles what
/// Network itself owns, so the Stage-4 handler stays an orchestrator rather than growing a
/// persistence body for every aggregate in the system.
///
/// Every path here is an upsert keyed on the provider's own identifier, which is what makes the
/// whole thing idempotent: uploading the same document twice resolves to the same rows and reports
/// zero changes. Nothing is ever hard-deleted — absence from a snapshot retires or archives, so the
/// record of what was once installed survives.
///
/// It does not call SaveChanges: everything it touches lives in NetworkDbContext, which the Stage-4
/// handler's unit of work commits. Alerts and Energy save their own contexts, because each module
/// declares its own IUnitOfWork — see <see cref="IEnergySyncExecutor"/> for what that means for
/// atomicity.
/// </summary>
internal sealed class SnapshotSyncApplier(
    ITowerRepository towers,
    ISiteEquipmentRepository equipment,
    IMaintenanceTicketRepository tickets,
    IEngineerRepository engineers,
    ILogger<SnapshotSyncApplier> logger)
{
    public async Task<SnapshotSyncCounts> ApplyAsync(
        IReadOnlyList<PipelineAction> actions,
        CancellationToken cancellationToken)
    {
        var counts = new SnapshotSyncCounts();

        foreach (UpsertTowerAction action in actions.OfType<UpsertTowerAction>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpsertTowerAsync(action, counts, cancellationToken);
        }

        foreach (SyncEquipmentAction action in actions.OfType<SyncEquipmentAction>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SyncEquipmentAsync(action, counts, cancellationToken);
        }

        foreach (SyncMaintenanceAction action in actions.OfType<SyncMaintenanceAction>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SyncMaintenanceAsync(action, counts, cancellationToken);
        }

        return counts;
    }

    private async Task UpsertTowerAsync(
        UpsertTowerAction action, SnapshotSyncCounts counts, CancellationToken ct)
    {
        Tower? tower = await towers.GetForUpdateAsync(action.TowerCode, ct);
        TowerStatus status = ParseStatus(action.StatusWire);

        if (tower is null)
        {
            // A site code we've never seen. Unlike the AI path — which may never invent a tower —
            // an operator feed reporting a site it owns is authoritative, so this creates one.
            if (action.Latitude is null || action.Longitude is null)
            {
                // Without coordinates the tower cannot be placed on the map. Better to skip and say
                // so than to drop a marker at (0,0) in the Gulf of Guinea.
                counts.Warn($"Site {action.TowerCode} is new but reported no coordinates — tower not created.");
                return;
            }

            Tower created = Tower.CreateFromSnapshot(
                code: action.TowerCode,
                name: action.Name,
                region: action.Region,
                latitude: action.Latitude.Value,
                longitude: action.Longitude.Value,
                signalPct: action.SignalPct ?? 0,
                loadPct: action.LoadPct ?? 0,
                status: status,
                issue: action.Issue);

            await towers.AddAsync(created, ct);
            counts.TowersCreated++;

            logger.LogInformation(
                "Snapshot created tower {TowerCode} ({Region}) at {Latitude},{Longitude}",
                action.TowerCode, action.Region, action.Latitude, action.Longitude);
            return;
        }

        // Identity/location and live metrics are separate authorities on the aggregate, so they are
        // applied separately — but a change to either counts as one tower update, not two.
        bool identityChanged = tower.ApplyIdentity(action.Name, action.Region, action.Latitude, action.Longitude);

        int signal = action.SignalPct ?? tower.SignalPct;
        int load = action.LoadPct ?? tower.LoadPct;
        bool metricsChanged =
            signal != tower.SignalPct ||
            load != tower.LoadPct ||
            status != tower.Status ||
            !string.Equals(tower.Issue, action.Issue, StringComparison.Ordinal);

        if (metricsChanged)
        {
            tower.UpdateMetrics(signal, load, status, action.Issue);
        }

        if (identityChanged || metricsChanged)
        {
            counts.TowerUpdates++;
        }
    }

    private async Task SyncEquipmentAsync(
        SyncEquipmentAction action, SnapshotSyncCounts counts, CancellationToken ct)
    {
        IReadOnlyList<SiteEquipment> existing = await equipment.ListForSiteAsync(action.SiteCode, ct);
        Dictionary<string, SiteEquipment> byId =
            existing.ToDictionary(e => e.EquipmentId, StringComparer.OrdinalIgnoreCase);

        var reportedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (EquipmentReport report in action.Reported)
        {
            reportedIds.Add(report.EquipmentId);

            if (byId.TryGetValue(report.EquipmentId, out SiteEquipment? unit))
            {
                if (unit.Observe(report.Type, report.Model, report.Status, action.ObservedAtUtc))
                {
                    counts.EquipmentUpdated++;
                }
                continue;
            }

            await equipment.AddAsync(
                SiteEquipment.Install(
                    action.SiteCode, report.EquipmentId, report.Type, report.Model, report.Status,
                    action.ObservedAtUtc),
                ct);
            counts.EquipmentCreated++;
        }

        // Anything installed at this site that the latest snapshot no longer lists has been
        // decommissioned or swapped out. Soft-retire it — the history of what was here matters.
        foreach (SiteEquipment unit in existing)
        {
            if (!reportedIds.Contains(unit.EquipmentId) && unit.Retire(action.ObservedAtUtc))
            {
                counts.EquipmentRetired++;
                logger.LogInformation(
                    "Equipment {EquipmentId} retired at {SiteCode} — absent from the latest snapshot",
                    unit.EquipmentId, action.SiteCode);
            }
        }
    }

    private async Task SyncMaintenanceAsync(
        SyncMaintenanceAction action, SnapshotSyncCounts counts, CancellationToken ct)
    {
        IReadOnlyList<MaintenanceTicket> existing = await tickets.ListForSiteAsync(action.SiteCode, ct);
        Dictionary<string, MaintenanceTicket> byId =
            existing.ToDictionary(t => t.TicketId, StringComparer.OrdinalIgnoreCase);

        var reportedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Open tickets ────────────────────────────────────────────────────────
        foreach (TicketReport report in action.OpenTickets)
        {
            reportedIds.Add(report.TicketId);

            // Register the assigned engineer first — but only when the feed gave a real id.
            if (!string.IsNullOrWhiteSpace(report.EngineerId) && !string.IsNullOrWhiteSpace(report.EngineerName))
            {
                await UpsertEngineerAsync(report.EngineerId, report.EngineerName, action.ObservedAtUtc, counts, ct);
            }

            if (byId.TryGetValue(report.TicketId, out MaintenanceTicket? ticket))
            {
                if (ticket.ObserveOpen(
                        report.Priority, report.ProviderStatus, report.Issue,
                        report.EngineerId, report.EngineerName, report.EstimatedArrival, action.ObservedAtUtc))
                {
                    counts.TicketsUpdated++;
                }
                continue;
            }

            await tickets.AddAsync(
                MaintenanceTicket.Open(
                    action.SiteCode, report.TicketId, report.Priority, report.ProviderStatus, report.Issue,
                    report.EngineerId, report.EngineerName, report.CreatedAt, report.EstimatedArrival,
                    action.ObservedAtUtc),
                ct);
            counts.TicketsCreated++;
        }

        // ── Completed work ──────────────────────────────────────────────────────
        // History can name a ticket we never saw open — the job was raised and finished between two
        // uploads. Create it already closed rather than dropping the record of work that was done.
        foreach (CompletedWorkReport report in action.CompletedWork)
        {
            reportedIds.Add(report.TicketId);

            if (byId.TryGetValue(report.TicketId, out MaintenanceTicket? ticket))
            {
                if (ticket.Complete(report.CompletedAt, report.EngineerName, report.Action, action.ObservedAtUtc))
                {
                    counts.TicketsCompleted++;
                }
                continue;
            }

            var backfilled = MaintenanceTicket.Open(
                action.SiteCode, report.TicketId,
                priority: null, providerStatus: null, issue: report.Action,
                assignedEngineerId: null, assignedEngineerName: report.EngineerName,
                createdAt: report.CompletedAt, estimatedArrival: null,
                seenAtUtc: action.ObservedAtUtc);

            backfilled.Complete(report.CompletedAt, report.EngineerName, report.Action, action.ObservedAtUtc);
            await tickets.AddAsync(backfilled, ct);
            counts.TicketsCompleted++;
        }

        // ── Fallen out of the feed ──────────────────────────────────────────────
        // Open here, but in neither list now. We are NOT told it was completed, so we must not say
        // it was — archive it and record that the provider stopped reporting it.
        foreach (MaintenanceTicket ticket in existing)
        {
            if (!reportedIds.Contains(ticket.TicketId) && ticket.Archive(action.ObservedAtUtc))
            {
                counts.TicketsArchived++;
                logger.LogInformation(
                    "Ticket {TicketId} archived at {SiteCode} — no longer reported as open or completed",
                    ticket.TicketId, action.SiteCode);
            }
        }
    }

    private async Task UpsertEngineerAsync(
        string engineerId, string name, DateTime observedAt, SnapshotSyncCounts counts, CancellationToken ct)
    {
        Engineer? existing = await engineers.GetByEngineerIdAsync(engineerId, ct);

        if (existing is null)
        {
            await engineers.AddAsync(Engineer.Register(engineerId, name, observedAt), ct);
            counts.EngineersCreated++;
            return;
        }

        if (existing.Observe(name, observedAt))
        {
            counts.EngineersUpdated++;
        }
    }

    private static TowerStatus ParseStatus(string wire) => wire?.ToUpperInvariant() switch
    {
        "CRITICAL" => TowerStatus.Critical,
        "WARN" or "WARNING" or "DEGRADED" => TowerStatus.Warn,
        _ => TowerStatus.Ok
    };
}

/// <summary>Mutable tally accumulated while applying one run's snapshot actions.</summary>
internal sealed class SnapshotSyncCounts
{
    private readonly List<string> _warnings = [];

    public int TowersCreated { get; set; }
    public int TowerUpdates { get; set; }
    public int EquipmentCreated { get; set; }
    public int EquipmentUpdated { get; set; }
    public int EquipmentRetired { get; set; }
    public int TicketsCreated { get; set; }
    public int TicketsUpdated { get; set; }
    public int TicketsCompleted { get; set; }
    public int TicketsArchived { get; set; }
    public int EngineersCreated { get; set; }
    public int EngineersUpdated { get; set; }

    public IReadOnlyList<string> Warnings => _warnings;

    public void Warn(string message) => _warnings.Add(message);
}
