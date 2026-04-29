using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Domain.Knowledge;
using Pgvector;

namespace Modules.Ai.Application.Rag.Stores;

/// <summary>
/// Persistence-side facade for the chunk index. The default implementation is
/// pgvector-backed; swapping in another vector store (Qdrant, Weaviate, ...)
/// is a single dependency injection change.
/// </summary>
public interface IKnowledgeStore
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Vector queryEmbedding,
        int topK,
        KnowledgeCategory? categoryFilter,
        string? regionFilter,
        CancellationToken cancellationToken = default);
}
