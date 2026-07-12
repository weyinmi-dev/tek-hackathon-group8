using System.ComponentModel;
using MediatR;
using Modules.Ai.Application.Knowledge;

namespace Modules.Ai.Agents.Tools;

/// <summary>Knowledge-base retrieval tool (Phase 2 §6.2). The one RAG surface exposed to the model.</summary>
public sealed class KnowledgeTools(ISender sender)
{
    [Description("Search the indexed telco knowledge base (past incident reports, outage summaries, engineering SOPs, tower/energy trends) for context relevant to the question. Use this for any 'why is X', 'has this happened before', or 'what's the runbook for Y' question.")]
    public Task<string> QueryKnowledge(
        [Description("The natural-language search query, e.g. 'fiber cut Lekki' or 'genset failover SOP'.")] string query,
        [Description("Optional category filter, e.g. 'incident_report', 'engineering_sop', 'energy_anomaly'. Empty for all.")] string category = "",
        [Description("Optional region filter, e.g. 'Lekki'. Empty for all.")] string region = "",
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(
            sender,
            new SearchKnowledgeQuery(
                query,
                TopK: null,
                Category: string.IsNullOrWhiteSpace(category) ? null : category,
                Region: string.IsNullOrWhiteSpace(region) ? null : region),
            cancellationToken);
}
