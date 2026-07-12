using Application.Abstractions.Messaging;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Application.Rag.Retrievers;
using Modules.Ai.Domain.Knowledge;
using SharedKernel;

namespace Modules.Ai.Application.Knowledge;

/// <summary>
/// Semantic search over the indexed knowledge corpus. Both the <c>query_knowledge</c> tool
/// (Phase 2 §6.2) and the MAF knowledge context provider (§8.2) dispatch this, so retrieval
/// rides the standard application pipeline instead of being called directly from the AI layer.
/// </summary>
public sealed record SearchKnowledgeQuery(
    string Query,
    int? TopK = null,
    string? Category = null,
    string? Region = null) : IQuery<IReadOnlyList<KnowledgeHitDto>>;

public sealed record KnowledgeHitDto(
    string SourceKey,
    string Category,
    string Region,
    string Title,
    string Content,
    double Similarity);

internal sealed class SearchKnowledgeQueryHandler(IRagRetriever retriever)
    : IQueryHandler<SearchKnowledgeQuery, IReadOnlyList<KnowledgeHitDto>>
{
    public async Task<Result<IReadOnlyList<KnowledgeHitDto>>> Handle(
        SearchKnowledgeQuery request, CancellationToken cancellationToken)
    {
        KnowledgeCategory? category = ParseCategory(request.Category);
        string? region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region;

        IReadOnlyList<RetrievedChunk> hits = await retriever.RetrieveAsync(
            request.Query, request.TopK, category, region, cancellationToken);

        IReadOnlyList<KnowledgeHitDto> results = hits
            .Select(h => new KnowledgeHitDto(
                h.SourceKey, h.Category.ToString(), h.Region, h.Title, h.Content, h.Similarity))
            .ToList();

        return Result.Success(results);
    }

    // Accepts both 'incident_report' and 'IncidentReport' forms — the model may emit either.
    private static KnowledgeCategory? ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }
        string normalized = category.Trim().Replace("_", "", StringComparison.Ordinal);
        return Enum.TryParse(normalized, ignoreCase: true, out KnowledgeCategory parsed) ? parsed : null;
    }
}
