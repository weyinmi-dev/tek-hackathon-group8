using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Application.Rag.Retrievers;

/// <summary>
/// Read side of the RAG pipeline. Embeds the query, runs a cosine similarity
/// search against the chunk index, and returns ranked rows with provenance.
/// Optional category / region filters narrow the search where the caller
/// already knows which slice of the corpus is relevant.
/// </summary>
public interface IRagRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int? topK = null,
        KnowledgeCategory? categoryFilter = null,
        string? regionFilter = null,
        CancellationToken cancellationToken = default);
}
