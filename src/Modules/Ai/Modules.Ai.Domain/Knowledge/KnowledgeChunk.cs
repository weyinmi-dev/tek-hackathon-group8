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
        float[] embedding) : base(id)
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
    /// The chunk's embedding as raw floats. Persisted to a pgvector <c>vector(N)</c> column by
    /// the infrastructure mapping (a value converter bridges <c>float[]</c> ↔ the pgvector type),
    /// so the domain carries no dependency on any vector library. Dimensionality matches whichever
    /// embedding model the indexer ran when the row was written (default 1536).
    /// </summary>
    public float[] Embedding { get; private set; } = null!;

    public static KnowledgeChunk Create(Guid documentId, int ordinal, string content, int tokenEstimate, float[] embedding) =>
        new(Guid.NewGuid(), documentId, ordinal, content, tokenEstimate, embedding);
}
