using Modules.Ai.Application.Rag.Models;

namespace Modules.Ai.Application.Rag.Indexing;

/// <summary>
/// Orchestrates the write side of the RAG pipeline: chunk → embed → upsert.
/// Idempotent on <c>SourceKey</c> — re-indexing the same document replaces
/// its chunks, so re-running the seeder is safe.
/// </summary>
public interface IRagIndexer
{
    Task<IndexResult> IndexAsync(KnowledgeDocumentInput document, CancellationToken cancellationToken = default);
    Task<IndexResult> IndexBatchAsync(IReadOnlyList<KnowledgeDocumentInput> documents, CancellationToken cancellationToken = default);
}
