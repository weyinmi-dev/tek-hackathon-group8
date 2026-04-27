using System.Diagnostics;
using Modules.Ai.Application.Copilot.AskCopilot;
using Modules.Ai.Application.SemanticKernel;
using Modules.Alerts.Api;
using Modules.Network.Api;

namespace Modules.Ai.Infrastructure.SemanticKernel;

/// <summary>
/// Deterministic, no-cost orchestrator used when Azure OpenAI is not configured.
/// Still hits the cross-module APIs (Network, Alerts) so the demo *feels* live —
/// the only thing being mocked is the LLM call itself. Returns the same
/// structured answer shape (ROOT CAUSE / AFFECTED / RECOMMENDED ACTIONS / CONFIDENCE)
/// the real LLM produces, so the frontend renderer is provider-agnostic.
/// </summary>
internal sealed class MockCopilotOrchestrator(INetworkApi network, IAlertsApi alerts) : ICopilotOrchestrator
{
    public async Task<CopilotAnswer> AskAsync(string query, string userRole, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var trace = new List<SkillTraceEntry>();

        // Skill chain — synthesized but with real (small) latencies for the agent panel.
        long t0 = sw.ElapsedMilliseconds;
        await Task.Delay(80, cancellationToken);
        trace.Add(new SkillTraceEntry("IntentParser", "parseQuery", (int)(sw.ElapsedMilliseconds - t0), "done"));

        long t1 = sw.ElapsedMilliseconds;
        IReadOnlyList<TowerSnapshot> towers = await network.ListTowersAsync(cancellationToken);
        trace.Add(new SkillTraceEntry("DiagnosticsSkill", "get_region_metrics", (int)(sw.ElapsedMilliseconds - t1), "done"));

        long t2 = sw.ElapsedMilliseconds;
        IReadOnlyList<AlertSnapshot> active = await alerts.ListActiveAsync(cancellationToken);
        trace.Add(new SkillTraceEntry("OutageSkill", "get_active_outages", (int)(sw.ElapsedMilliseconds - t2), "done"));

        long t3 = sw.ElapsedMilliseconds;
        await Task.Delay(60, cancellationToken);
        trace.Add(new SkillTraceEntry("RecommendationSkill", "suggest_actions", (int)(sw.ElapsedMilliseconds - t3), "done"));

        // Pick the most-impactful active alert as the focal point.
        AlertSnapshot? focal = active
            .OrderByDescending(a => SeverityRank(a.Severity))
            .ThenByDescending(a => a.SubscribersAffected)
            .FirstOrDefault();

        TowerSnapshot? focalTower = focal is null ? null : towers.FirstOrDefault(t => t.Code == focal.TowerCode.Split(' ')[0]);

        string telemetry = focalTower is null
            ? "metric anomalies"
            : $"signal {focalTower.SignalPct}% / load {focalTower.LoadPct}%";

        string answer = focal is null
            ? "ROOT CAUSE\nNo critical incidents detected. Network is operating within nominal SLA bounds.\n\nAFFECTED\n• None\n\nRECOMMENDED ACTIONS\n1. Continue monitoring\n2. Run weekly health probe\n3. Review forward capacity plan\n\nCONFIDENCE\n95 % — full telemetry coverage, no anomalies."
            : $"""
            ROOT CAUSE
            {focal.Cause} on {focal.TowerCode} ({focalTower?.Name ?? focal.Region}). Tower telemetry shows {telemetry}. Pattern matches the {focal.Severity} class (incident {focal.Code}).

            AFFECTED
            • {focal.Region} — {focal.SubscribersAffected:N0} subscribers
            • Spillover risk to neighbouring cells in the same region cluster
            • Confidence in attribution: {focal.Confidence:P0}

            RECOMMENDED ACTIONS
            {SuggestActionsFor(focal.Cause, focal.TowerCode)}

            CONFIDENCE
            {(int)(focal.Confidence * 100)} % — derived from telemetry correlation across {towers.Count} towers and {active.Count} active incidents.
            """;

        return new CopilotAnswer(
            Answer: answer,
            Confidence: focal?.Confidence ?? 0.95,
            SkillTrace: trace,
            Attachments: AttachmentSelector.Select(query),
            Provider: "mock");
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 2,
        "warn" => 1,
        _ => 0,
    };

    private static string SuggestActionsFor(string cause, string towerCode)
    {
        const StringComparison ic = StringComparison.OrdinalIgnoreCase;
        return cause switch
        {
            var x when x.Contains("fiber", ic) => $"1. Dispatch field-team-3 to nearest fiber junction serving {towerCode} (ETA <30 min)\n2. Auto-shed traffic from {towerCode} → adjacent towers in the same region\n3. Open ticket with civil-works contractor — request immediate halt",
            var x when x.Contains("power", ic) || x.Contains("grid", ic) => $"1. Engage genset failover for the affected cluster within 15 min\n2. Notify IKEDC of impacted feeder\n3. Pre-position fuel for next 12h",
            var x when x.Contains("congest", ic) || x.Contains("load", ic) => $"1. Trigger automated load-shed onto idle neighbouring cells\n2. Reprioritise voice-over-data scheduling for {towerCode}\n3. Open capacity ticket — add carrier or upgrade backhaul",
            var x when x.Contains("predict", ic) || x.Contains("thermal", ic) => $"1. Schedule preventive maintenance window in next 2h\n2. Pre-provision spare amplifier for hot-swap\n3. Subscribe NOC to thermal-trend alerts at 80% threshold",
            _ => $"1. Open P2 ticket and assign Tier-2 NOC on-call\n2. Capture 5-minute telemetry snapshot for impacted segment\n3. If subscriber impact >5k, notify customer-care and post status page",
        };
    }
}
