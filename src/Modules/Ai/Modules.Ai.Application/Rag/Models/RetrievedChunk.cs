using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Application.Rag.Models;

/// <summary>One result row from a similarity search — content + provenance + score.</summary>
public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string SourceKey,
    KnowledgeCategory Category,
    string Title,
    string Region,
    int Ordinal,
    string Content,
    double Similarity);
