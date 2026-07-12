using Application.Abstractions.Messaging;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain.Knowledge;
using SharedKernel;

namespace Modules.Ai.Application.Ingestion;

/// <summary>
/// One embedded chunk ready to persist. Carries the vector as a plain <c>float[]</c> so the command
/// contract stays free of the pgvector type (that mapping lives in Infrastructure — Phase 2 §3.2).
/// </summary>
public sealed record ChunkEmbedding(int Ordinal, string Content, int TokenEstimate, float[] Embedding);

/// <summary>
/// The final, repo-touching step of <c>DocumentIngestionWorkflow</c>: writes the knowledge document
/// and its chunks, then flips the managed document to Indexed — atomically, in one unit of work.
/// The workflow executor dispatches this via ISender so the Agents layer never touches a repository
/// (Phase 2 §4.1). Idempotent on <paramref name="SourceKey"/>: a re-run replaces the same chunks
/// rather than duplicating them, which is what makes a resumed ingestion safe.
/// </summary>
public sealed record PersistDocumentKnowledgeCommand(
    Guid DocumentId,
    string SourceKey,
    KnowledgeCategory Category,
    string Title,
    string Region,
    string Tags,
    string Body,
    DateTime OccurredAtUtc,
    IReadOnlyList<ChunkEmbedding> Chunks) : ICommand<int>;

internal sealed class PersistDocumentKnowledgeCommandHandler(
    IManagedDocumentRepository documents,
    IKnowledgeRepository knowledge,
    IUnitOfWork uow) : ICommandHandler<PersistDocumentKnowledgeCommand, int>
{
    public async Task<Result<int>> Handle(PersistDocumentKnowledgeCommand request, CancellationToken cancellationToken)
    {
        ManagedDocument? doc = await documents.GetByIdAsync(request.DocumentId, cancellationToken);
        if (doc is null)
        {
            return Result.Failure<int>(Error.NotFound(
                "Document.NotFound", $"Document {request.DocumentId} was not found."));
        }

        // SourceKey ties idempotency back to the managed document (doc:{id}:v{version}); re-running
        // replaces the same knowledge document rather than creating a duplicate.
        KnowledgeDocument? existing = await knowledge.FindBySourceKeyAsync(request.SourceKey, cancellationToken);
        KnowledgeDocument document;
        if (existing is null)
        {
            document = KnowledgeDocument.Create(
                request.SourceKey, request.Category, request.Title, request.Region,
                request.Body, request.Tags, request.OccurredAtUtc);
            await knowledge.AddDocumentAsync(document, cancellationToken);
        }
        else
        {
            document = existing;
            document.Replace(request.Title, request.Region, request.Body, request.Tags, request.OccurredAtUtc);
        }

        List<KnowledgeChunk> chunks = request.Chunks
            .Select(c => KnowledgeChunk.Create(document.Id, c.Ordinal, c.Content, c.TokenEstimate, c.Embedding))
            .ToList();
        await knowledge.ReplaceChunksAsync(document.Id, chunks, cancellationToken);

        doc.MarkIndexed(document.Id);
        await uow.SaveChangesAsync(cancellationToken);
        return Result.Success(chunks.Count);
    }
}
