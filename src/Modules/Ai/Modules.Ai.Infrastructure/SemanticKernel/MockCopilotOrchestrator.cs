using System.Diagnostics;
using System.Globalization;
using System.Text;
using Modules.Ai.Application.Copilot.AskCopilot;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Application.Rag.Retrievers;
using Modules.Ai.Application.SemanticKernel;
using Modules.Alerts.Api;
using Modules.Network.Api;
using Modules.Network.Domain.Runbooks;

namespace Modules.Ai.Infrastructure.SemanticKernel;

/// <summary>
/// Deterministic, no-cost orchestrator used when Azure OpenAI is not configured.
/// Still hits the cross-module APIs (Network, Alerts) AND the RAG pipeline so the
/// demo *feels* live — the only thing being mocked is the LLM call itself. Returns
/// the same structured answer shape (ROOT CAUSE / AFFECTED / RECOMMENDED ACTIONS /
/// CONFIDENCE) the real LLM produces, so the frontend renderer stays provider-agnostic.
/// </summary>
internal sealed class MockCopilotOrchestrator(
    INetworkApi network,
    IAlertsApi alerts,
    IRagRetriever retriever) : ICopilotOrchestrator
{
    public async Task<CopilotAnswer> AskAsync(string query, string userRole, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var trace = new List<SkillTraceEntry>();

        long t0 = sw.ElapsedMilliseconds;
        await Task.Delay(80, cancellationToken);
        trace.Add(new SkillTraceEntry("IntentParser", "parseQuery", (int)(sw.ElapsedMilliseconds - t0), "done"));

        long tRag = sw.ElapsedMilliseconds;
        IReadOnlyList<RetrievedChunk> ragHits = await retriever.RetrieveAsync(query, topK: 4, cancellationToken: cancellationToken);
        trace.Add(new SkillTraceEntry("KnowledgeSkill", "search_knowledge", (int)(sw.ElapsedMilliseconds - tRag), ragHits.Count > 0 ? "done" : "empty"));

        long t1 = sw.ElapsedMilliseconds;
        IReadOnlyList<TowerSnapshot> towers = await network.ListTowersAsync(cancellationToken);
        trace.Add(new SkillTraceEntry("DiagnosticsSkill", "get_region_metrics", (int)(sw.ElapsedMilliseconds - t1), "done"));

        long t2 = sw.ElapsedMilliseconds;
        IReadOnlyList<AlertSnapshot> active = await alerts.ListActiveAsync(cancellationToken);
        trace.Add(new SkillTraceEntry("OutageSkill", "get_active_outages", (int)(sw.ElapsedMilliseconds - t2), "done"));

        long t3 = sw.ElapsedMilliseconds;
        await Task.Delay(60, cancellationToken);
        trace.Add(new SkillTraceEntry("RecommendationSkill", "suggest_actions", (int)(sw.ElapsedMilliseconds - t3), "done"));

        AlertSnapshot? focal = active
            .OrderByDescending(a => SeverityRank(a.Severity))
            .ThenByDescending(a => a.SubscribersAffected)
            .FirstOrDefault();

        TowerSnapshot? focalTower = focal is null ? null : towers.FirstOrDefault(t => t.Code == focal.TowerCode.Split(' ')[0]);

        string telemetry = focalTower is null
            ? "metric anomalies"
            : $"signal {focalTower.SignalPct}% / load {focalTower.LoadPct}%";

        string ragSection = BuildRagSection(ragHits);

        string answer = focal is null
            ? $"""
            ROOT CAUSE
            No critical incidents detected. Network is operating within nominal SLA bounds.

            AFFECTED
            • None

            RECOMMENDED ACTIONS
            1. Continue monitoring
            2. Run weekly health probe
            3. Review forward capacity plan

            {ragSection}CONFIDENCE
            95 % — full telemetry coverage, no anomalies.
            """
            : $"""
            ROOT CAUSE
            {focal.Cause} on {focal.TowerCode} ({focalTower?.Name ?? focal.Region}). Tower telemetry shows {telemetry}. Pattern matches the {focal.Severity} class (incident {focal.Code}).

            AFFECTED
            • {focal.Region} — {focal.SubscribersAffected:N0} subscribers
            • Spillover risk to neighbouring cells in the same region cluster
            • Confidence in attribution: {focal.Confidence.ToString("P0", CultureInfo.InvariantCulture)}

            RECOMMENDED ACTIONS
            {RunbookPolicy.Recommend(focal.Cause, focal.TowerCode)}

            {ragSection}CONFIDENCE
            {(int)(focal.Confidence * 100)} % — derived from telemetry correlation across {towers.Count} towers, {active.Count} active incidents, and {ragHits.Count} historical knowledge-base matches.
            """;

        return new CopilotAnswer(
            Answer: answer,
            Confidence: focal?.Confidence ?? 0.95,
            SkillTrace: trace,
            Attachments: AttachmentSelector.Select(query),
            Provider: "mock");
    }

    private static string BuildRagSection(IReadOnlyList<RetrievedChunk> hits)
    {
        if (hits.Count == 0)
        {
            return "HISTORICAL CONTEXT\nNo relevant historical reports found in the knowledge base.\n\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("HISTORICAL CONTEXT");
        sb.AppendLine($"Retrieved {hits.Count} relevant knowledge-base matches:");
        foreach (RetrievedChunk h in hits.Take(3))
        {
            sb.Append("  • [").Append(h.SourceKey).Append("] (").Append(h.Category).Append(") ").Append(h.Title)
              .Append(" — ").Append(h.Region).AppendLine();
            // Optional: peek at the content if it's short
            string snippet = h.Content.Length > 120 ? h.Content[..120] + "..." : h.Content;
            sb.Append("    \"").Append(snippet.Replace("\n", " ")).AppendLine("\"");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 2,
        "warn" => 1,
        _ => 0,
    };
}
