using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Infrastructure.Rag.Seed;

/// <summary>
/// Boots the demo telco knowledge corpus on first run. Covers the six data
/// sources called out in docs/instructions.md (incident reports, outage
/// summaries, network diagnostics, engineering SOPs, tower performance,
/// alert history). The seeder is idempotent — it skips when the table is
/// already populated, so re-running is safe.
/// </summary>
public static class KnowledgeCorpusSeeder
{
    public static async Task SeedAsync(
        IRagIndexer indexer,
        IKnowledgeRepository knowledge,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        int existing = await knowledge.CountDocumentsAsync(cancellationToken);
        if (existing > 0)
        {
            logger.LogDebug("Knowledge corpus already seeded ({Count} docs) — skipping.", existing);
            return;
        }

        IReadOnlyList<KnowledgeDocumentInput> corpus = BuildCorpus();
        IndexResult result = await indexer.IndexBatchAsync(corpus, cancellationToken);

        logger.LogInformation("Seeded telco knowledge corpus: {Docs} documents → {Chunks} chunks indexed.",
            result.DocumentsIndexed, result.ChunksIndexed);
    }

    private static IReadOnlyList<KnowledgeDocumentInput> BuildCorpus()
    {
        DateTime now = DateTime.UtcNow;

        return
        [
            new KnowledgeDocumentInput(
                SourceKey: "INC-2841-WRITEUP",
                Category: KnowledgeCategory.IncidentReport,
                Title: "Lekki Phase 1 — TWR-LEK-003 backhaul fiber cut",
                Region: "Lekki",
                Body: """
                Incident INC-2841 — Lekki Phase 1, 14,200 subscribers impacted.

                ROOT CAUSE
                Backhaul fiber TG-LEK-A degraded between 16:48 and 17:02 — packet loss climbed
                from 2% to 60% in 12 minutes. Civil-works permit was issued in the area at 16:50,
                a strong correlation with fiber-cut events catalogued in the previous quarter.

                AFFECTED
                TWR-LEK-003 (Lekki Phase 1) — 14,200 subscribers.
                Spillover to TWR-LEK-008 at 88% load.
                4G voice + data; no impact on 5G NSA.

                RESOLUTION
                Field-team-3 dispatched to fiber junction LJ-7. Splice repair completed in 38 min.
                Auto-shed traffic to TWR-LEK-008 and TWR-LEK-014 reduced subscriber impact during repair.
                """,
                Tags: ["fiber", "backhaul", "lekki", "twr-lek-003", "tg-lek-a", "fiber-cut"],
                OccurredAtUtc: now.AddDays(-3)),

            new KnowledgeDocumentInput(
                SourceKey: "INC-2840-WRITEUP",
                Category: KnowledgeCategory.IncidentReport,
                Title: "Lagos West cluster — IKEDC sector 7 grid failure",
                Region: "Lagos West",
                Body: """
                Incident INC-2840 — Lagos West, 38,400 subscribers impacted across 3 towers.

                ROOT CAUSE
                Grid failure on IKEDC sector 7 feeder. TWR-LAG-W-014 and two adjacent towers fell
                offline within a 90-second window. Genset failover was inhibited because two of the
                three sites had pending maintenance flags.

                RECOVERY
                Manual genset start completed within 14 min. Notification dispatched to IKEDC NOC.
                Fuel pre-positioning policy updated post-incident: any cluster with >2 towers on the
                same feeder now keeps a 12h fuel reserve.
                """,
                Tags: ["power", "grid", "ikedc", "lagos-west", "genset", "outage"],
                OccurredAtUtc: now.AddDays(-2)),

            new KnowledgeDocumentInput(
                SourceKey: "OUTAGE-Q1-RECAP",
                Category: KnowledgeCategory.OutageSummary,
                Title: "Q1 outage summary — Lagos metro",
                Region: "Lagos",
                Body: """
                Lagos metro recorded 47 P1 / P2 outages in Q1. Top causes by frequency:
                  1. Fiber cuts (38%) — concentrated in Lekki and VI corridors with active civil works.
                  2. Power / grid failures (29%) — Lagos West most exposed; IKEDC sectors 5, 7, 11.
                  3. Congestion (17%) — Festac and Surulere during evening peak hours.
                  4. Thermal / hardware (9%) — clustered in Ikeja and Allen Avenue legacy sites.
                  5. Microwave jitter (7%) — weather-correlated, predominantly Q1 rains.

                Mean time to detect dropped from 8.4 to 3.2 minutes after Smart Alerts rollout.
                Mean time to mitigate is now 22 minutes for fiber-class events with field-team-3 on call.
                """,
                Tags: ["summary", "q1", "lagos", "metro", "fiber-cut", "power", "congestion"],
                OccurredAtUtc: now.AddDays(-30)),

            new KnowledgeDocumentInput(
                SourceKey: "OUTAGE-LEKKI-CORRIDOR-2025",
                Category: KnowledgeCategory.OutageSummary,
                Title: "Lekki corridor outage history (last 12 weeks)",
                Region: "Lekki",
                Body: """
                Lekki Phase 1 + 2 saw 11 fiber-class incidents in the trailing 12 weeks. All but two
                were on the TG-LEK-A backhaul ring serving TWR-LEK-003, TWR-LEK-008 and TWR-LEK-014.
                Civil-works permits in the corridor are the dominant external driver. Average
                subscriber impact per fiber event: 11,800. Reroute via TG-LEK-B is available but
                doubles latency on voice calls.
                """,
                Tags: ["lekki", "tg-lek-a", "fiber", "history", "tg-lek-b"],
                OccurredAtUtc: now.AddDays(-7)),

            new KnowledgeDocumentInput(
                SourceKey: "DIAG-LATENCY-PLAYBOOK",
                Category: KnowledgeCategory.NetworkDiagnostic,
                Title: "Latency triage — diagnostic order of operations",
                Region: "Lagos",
                Body: """
                When users report regional slowness, run the diagnostic in this order:
                  1. Pull the live tower snapshot (signal, load, status) for the named region.
                  2. Cross-check active outages — a single critical incident often explains everything.
                  3. If the cluster is healthy but one tower is hot (load >85%), spillover candidate
                     is congestion. Trigger automated load-shed first.
                  4. If multiple towers show signal <70% and at least one offline, suspect backhaul
                     or power. Pivot to fiber/power runbook.
                  5. If all metrics are nominal but users still report, suspect device-side or RAN
                     scheduler — capture a 5-minute deep telemetry snapshot before escalating.
                """,
                Tags: ["latency", "playbook", "diagnostic", "triage", "lagos"],
                OccurredAtUtc: now.AddDays(-45)),

            new KnowledgeDocumentInput(
                SourceKey: "DIAG-CONGESTION-PATTERNS",
                Category: KnowledgeCategory.NetworkDiagnostic,
                Title: "Congestion patterns — Festac & Surulere evening peak",
                Region: "Festac",
                Body: """
                Festac (TWR-OJO-002) and Surulere consistently trip the 85% load threshold between
                18:30 and 21:30 on weekdays. The pattern correlates with residential streaming peak.
                Recommended posture: keep TWR-OJO-002 in pre-shed mode after 18:00; use TWR-OJO-005
                and TWR-LAG-W-022 as failover targets — both have evening capacity headroom of >40%.
                """,
                Tags: ["festac", "surulere", "congestion", "load-shed", "twr-ojo-002"],
                OccurredAtUtc: now.AddDays(-9)),

            new KnowledgeDocumentInput(
                SourceKey: "SOP-FIBER-CUT-V3",
                Category: KnowledgeCategory.EngineeringSop,
                Title: "SOP: fiber cut on backhaul ring",
                Region: "Lagos",
                Body: """
                Standard operating procedure — backhaul fiber cut.

                STEP 1. Confirm via two independent telemetry sources (packet loss + signal drop).
                STEP 2. Issue an automated traffic-shed from the impacted tower(s) to the nearest
                        two healthy neighbours in the same region cluster.
                STEP 3. Dispatch field-team-3 (24/7, ETA <30 min for any Lagos site) to the fiber
                        junction with the highest correlation to the affected segment. LJ-7 serves
                        the Lekki corridor; LJ-12 serves Lagos West; LJ-3 serves VI.
                STEP 4. Open a ticket with the civil-works contractor for the area and request an
                        immediate halt if works are in progress.
                STEP 5. After splice completion, verify with a 5-minute soak before clearing.
                """,
                Tags: ["sop", "fiber-cut", "runbook", "field-team-3", "lj-7", "lj-12", "lj-3"],
                OccurredAtUtc: now.AddDays(-90)),

            new KnowledgeDocumentInput(
                SourceKey: "SOP-POWER-FAILOVER-V2",
                Category: KnowledgeCategory.EngineeringSop,
                Title: "SOP: grid failure / genset failover",
                Region: "Lagos",
                Body: """
                Standard operating procedure — DisCo / grid failure with genset failover.

                STEP 1. Auto-engage genset on the affected cluster within 15 min of grid alarm.
                        Verify the start sequence completed (controller status, fuel pressure).
                STEP 2. Notify IKEDC / EKEDC NOC of the impacted feeder via the NOC API.
                STEP 3. Pre-position fuel for next 12 h based on the rolling fault duration trend.
                        Default 12 h if no historical data available.
                STEP 4. If two or more sites on the same feeder are affected, escalate to Tier-2
                        and open a regulatory incident report.
                """,
                Tags: ["sop", "power", "grid", "genset", "ikedc", "ekedc", "failover"],
                OccurredAtUtc: now.AddDays(-120)),

            new KnowledgeDocumentInput(
                SourceKey: "SOP-CONGESTION-LOAD-SHED",
                Category: KnowledgeCategory.EngineeringSop,
                Title: "SOP: congestion load-shed",
                Region: "Lagos",
                Body: """
                Standard operating procedure — sustained tower load >85%.

                STEP 1. Trigger automated load-shed onto idle neighbouring cells in the same cluster.
                STEP 2. Reprioritise voice over data scheduling on the impacted tower.
                STEP 3. If load remains >85% after shedding, open a capacity ticket — request
                        additional carrier or backhaul upgrade. Tag with the region and tower codes.
                """,
                Tags: ["sop", "congestion", "load-shed", "capacity"],
                OccurredAtUtc: now.AddDays(-60)),

            new KnowledgeDocumentInput(
                SourceKey: "TOWER-PERF-LEK-003",
                Category: KnowledgeCategory.TowerPerformance,
                Title: "TWR-LEK-003 performance trend (90 d)",
                Region: "Lekki",
                Body: """
                TWR-LEK-003 (Lekki Phase 1) — 90-day performance summary.

                Average signal 91%; average load 67% on weekdays, 78% on weekends.
                Five fiber-class incidents tied to TG-LEK-A backhaul. Mean time to repair 41 min.
                Thermal trend stable. Spillover candidate TWR-LEK-008 carried 22% of LEK-003 traffic
                during outages with no SLA breach. Recommended posture: keep TWR-LEK-008 in
                hot-failover mode whenever LEK-003 is in alarm.
                """,
                Tags: ["twr-lek-003", "performance", "lekki", "trend", "twr-lek-008"],
                OccurredAtUtc: now.AddDays(-1)),

            new KnowledgeDocumentInput(
                SourceKey: "TOWER-PERF-LAG-W-031",
                Category: KnowledgeCategory.TowerPerformance,
                Title: "TWR-LAG-W-031 thermal trend warning",
                Region: "Lagos West",
                Body: """
                TWR-LAG-W-031 — predicted-failure window noted in INC-2839.
                Thermal sensor average climbed from 58 °C to 71 °C over the trailing 14 days.
                Combined with peak load >82%, the model gives an 87% probability of fault by
                end-of-day. Recommended action: schedule preventive maintenance window in the
                next 2 hours, pre-provision a spare amplifier for hot-swap, and subscribe NOC
                to thermal-trend alerts at the 80% threshold for early warning.
                """,
                Tags: ["twr-lag-w-031", "thermal", "predicted-failure", "preventive-maintenance"],
                OccurredAtUtc: now.AddHours(-6)),

            new KnowledgeDocumentInput(
                SourceKey: "ALERT-HISTORY-IKEJA-Q1",
                Category: KnowledgeCategory.AlertHistory,
                Title: "Ikeja alert history — Q1",
                Region: "Ikeja",
                Body: """
                Ikeja recorded 6 alerts in Q1, all auto-resolved by the load-shed automation.
                TWR-IKJ-019 latency anomaly cleared after spillover to TWR-IKJ-021. No subscriber
                impact. The Ikeja cluster has the strongest mesh redundancy in the Lagos metro
                and is the gold reference for capacity headroom planning.
                """,
                Tags: ["ikeja", "alerts", "history", "twr-ikj-019", "twr-ikj-021", "auto-resolved"],
                OccurredAtUtc: now.AddDays(-20)),

            new KnowledgeDocumentInput(
                SourceKey: "ALERT-HISTORY-FESTAC-2025",
                Category: KnowledgeCategory.AlertHistory,
                Title: "Festac alert history — last 30 days",
                Region: "Festac",
                Body: """
                Festac generated 14 alerts in the last 30 days, dominated by crowd-sourced
                signal-drop reports clustered around TWR-OJO-002. Most cleared without operator
                action once load-shed engaged. One Tier-2 escalation occurred when TWR-OJO-002
                and TWR-OJO-005 hit 92% load simultaneously during a public holiday.
                Capacity uplift planned for next quarter.
                """,
                Tags: ["festac", "alerts", "history", "twr-ojo-002", "crowd-sourced"],
                OccurredAtUtc: now.AddDays(-12)),
        ];
    }
}
