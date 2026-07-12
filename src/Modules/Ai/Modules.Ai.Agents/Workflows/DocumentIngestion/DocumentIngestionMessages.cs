using SharedKernel;
using Modules.Ai.Application.Rag.Chunking;

namespace Modules.Ai.Agents.Workflows.DocumentIngestion;

// The messages that flow along DocumentIngestionWorkflow's edges. Each executor receives one and
// returns the next; MAF checkpoints these in-flight messages at every superstep boundary, so a
// resume picks up from the last completed step with its result already in hand (e.g. a resume after
// EmbedChunks carries the vectors forward and never re-embeds). Keeping the state in the messages —
// not in executor fields — is what makes the executors stateless and the resume correct.

/// <summary>
/// Workflow input. Assembled by the trigger (M9) from the ManagedDocument after it has been marked
/// InProgress, so the executors need only the descriptor — never a repository — to do their work.
/// </summary>
public sealed record IngestDocumentRequest(
    Guid DocumentId,
    DocumentSource Source,
    string StorageKey,
    string ContentType,
    string FileName,
    string Title,
    string Region,
    string Tags,
    KnowledgeCategory Category,
    DateTime OccurredAtUtc,
    int Version)
{
    /// <summary>Idempotency key — a re-run replaces the same knowledge document instead of duplicating it.</summary>
    public string SourceKey => $"doc:{DocumentId}:v{Version}";
}

public sealed record ExtractedText(IngestDocumentRequest Doc, bool HasText, string Text);

public sealed record ValidationResult(IngestDocumentRequest Doc, string Text, bool Relevant, string Reason);

public sealed record ChunkedText(IngestDocumentRequest Doc, string Text, IReadOnlyList<TextChunk> Chunks);

public sealed record EmbeddedText(
    IngestDocumentRequest Doc,
    string Text,
    IReadOnlyList<TextChunk> Chunks,
    IReadOnlyList<float[]> Vectors);

/// <summary>Terminal output — Indexed, Rejected, or Failed — surfaced by the workflow.</summary>
public sealed record IngestionCompleted(Guid DocumentId, string Status, int ChunkCount);
