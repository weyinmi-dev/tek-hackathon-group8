namespace Modules.Ai.Application.Rag;

/// <summary>
/// RAG sub-section of the <c>Ai</c> configuration block.
/// Bound from <c>Ai:Rag:*</c> at startup. Defaults are tuned for the demo corpus.
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Ai:Rag";

    /// <summary>Master switch. When false, the orchestrators skip retrieval entirely.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Number of chunks the retriever returns for each query.</summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Output dimensionality of the embedding model. Must match the column type
    /// of <c>knowledge_chunks.embedding</c> at index time. text-embedding-3-small
    /// returns 1536; -3-large returns 3072.
    /// </summary>
    public int EmbeddingDimensions { get; init; } = 1536;

    /// <summary>Approximate maximum characters per chunk before splitting.</summary>
    public int ChunkSize { get; init; } = 600;

    /// <summary>Characters of overlap between consecutive chunks (preserves cross-boundary context).</summary>
    public int ChunkOverlap { get; init; } = 80;

    /// <summary>
    /// Auto-seed the demo telco corpus on first run when the table is empty.
    /// Ignored when the table already has rows.
    /// </summary>
    public bool AutoSeedCorpus { get; init; } = true;
}
