using Pgvector;

namespace Modules.Ai.Application.Rag.Embeddings;

/// <summary>
/// Reusable embedding abstraction. The Azure OpenAI implementation calls
/// <c>text-embedding-3-small</c> (or whatever deployment is configured); the
/// mock implementation produces deterministic hash-based pseudo-embeddings so
/// the RAG pipeline still works end-to-end without model credentials.
///
/// Per docs/instructions.md: "DO NOT hardcode embedding logic into controllers."
/// All consumers depend on this interface, never on the implementation.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>Number of dimensions emitted by this generator. Must match the column type at index time.</summary>
    int Dimensions { get; }

    /// <summary>Logical name surfaced for telemetry / debugging.</summary>
    string ModelName { get; }

    Task<Vector> GenerateAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vector>> GenerateBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);
}
