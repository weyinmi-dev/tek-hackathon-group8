using Pgvector;
using SharedKernel;

namespace Modules.Ai.Domain.Knowledge;

/// <summary>
/// A single retrieval-sized window cut out of a <see cref="KnowledgeDocument"/>.
/// Embedding lives on the chunk because chunk-level recall is what the
/// retriever scores against; we then group/rerank up to the document.
/// </summary>
public sealed class KnowledgeChunk : Entity
{
    private KnowledgeChunk(
        Guid id,
        Guid documentId,
        int ordinal,
        string content,
        int tokenEstimate,
        Vector embedding) : base(id)
    {
        DocumentId = documentId;
        Ordinal = ordinal;
        Content = content;
        TokenEstimate = tokenEstimate;
        Embedding = embedding;
    }

    private KnowledgeChunk() { }

    public Guid DocumentId { get; private set; }
    public int Ordinal { get; private set; }
    public string Content { get; private set; } = null!;
    public int TokenEstimate { get; private set; }

    /// <summary>
    /// Stored as a pgvector column. Dimensionality matches whichever embedding model the
    /// indexer was running when the row was written (default 1536 — text-embedding-3-small).
    /// </summary>
    public Vector Embedding { get; private set; } = null!;

    public static KnowledgeChunk Create(Guid documentId, int ordinal, string content, int tokenEstimate, Vector embedding) =>
        new(Guid.NewGuid(), documentId, ordinal, content, tokenEstimate, embedding);
}
