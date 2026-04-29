using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.SemanticKernel;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Application.Rag.Retrievers;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill — exposes the RAG retriever to the LLM as <c>search_knowledge</c>.
/// Returns a compact, citation-friendly view of the top-K matching chunks
/// (incident reports, runbooks, tower performance summaries, etc.) so the
/// model can ground answers in real telco context instead of guessing.
/// </summary>
public sealed class KnowledgeSkill(IRagRetriever retriever)
{
    [KernelFunction("search_knowledge")]
    [Description("Search the indexed telco knowledge base (past incident reports, outage summaries, engineering SOPs, tower performance trends) for context relevant to the user's question. Use this for any 'why is X slow', 'has this happened before', or 'what's the runbook for Y' query.")]
    public async Task<string> SearchAsync(
        [Description("The natural-language search query — e.g. 'fiber cut Lekki', 'genset failover SOP', 'Festac evening congestion'.")] string query,
        [Description("Optional category filter: 'incident_report', 'outage_summary', 'network_diagnostic', 'engineering_sop', 'tower_performance', 'alert_history'. Omit or pass empty for all categories.")] string category = "",
        [Description("Optional region filter, e.g. 'Lekki', 'Lagos West', 'Ikeja'. Omit for all regions.")] string region = "",
        CancellationToken cancellationToken = default)
    {
        KnowledgeCategory? cat = ParseCategory(category);
        string? regionFilter = string.IsNullOrWhiteSpace(region) ? null : region;

        IReadOnlyList<RetrievedChunk> hits = await retriever
            .RetrieveAsync(query, topK: null, cat, regionFilter, cancellationToken);

        if (hits.Count == 0)
        {
            return "No relevant knowledge-base entries found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Top {hits.Count} knowledge-base matches:");
        foreach (RetrievedChunk h in hits)
        {
            sb.Append("- [")
              .Append(h.SourceKey).Append("] (").Append(h.Category).Append(", ").Append(h.Region)
              .Append(", similarity=").Append(h.Similarity.ToString("F3", CultureInfo.InvariantCulture)).Append(") ")
              .Append(h.Title).AppendLine();
            sb.Append("  ").AppendLine(h.Content.Replace("\n", "\n  ", StringComparison.Ordinal));
        }
        return sb.ToString();
    }

    private static KnowledgeCategory? ParseCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }
        string normalized = category.Trim().Replace("_", "", StringComparison.Ordinal);
        return Enum.TryParse<KnowledgeCategory>(normalized, ignoreCase: true, out KnowledgeCategory parsed)
            ? parsed
            : null;
    }
}
