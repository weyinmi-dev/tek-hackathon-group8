using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Chunking;
using Modules.Ai.Application.Rag.Embeddings;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Knowledge;
using Pgvector;

namespace Modules.Ai.Infrastructure.Rag.Indexing;

/// <summary>
/// Orchestrates the indexing pipeline: chunk → embed (in one batch per
/// document) → upsert document → replace chunks → save. Idempotent on
/// <c>SourceKey</c>: re-running with the same key replaces the chunks.
/// </summary>
internal sealed class RagIndexer(
    IChunker chunker,
    IEmbeddingGenerator embeddings,
    IKnowledgeRepository knowledge,
    IUnitOfWork uow,
    ILogger<RagIndexer> logger) : IRagIndexer
{
    public async Task<IndexResult> IndexAsync(KnowledgeDocumentInput document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        IReadOnlyList<TextChunk> chunks = chunker.Split(document.Body);
        if (chunks.Count == 0)
        {
            logger.LogWarning("Skipping indexing for {SourceKey} — body produced zero chunks.", document.SourceKey);
            return new IndexResult(0, 0);
        }

        IReadOnlyList<Vector> vectors = await embeddings
            .GenerateBatchAsync(chunks.Select(c => c.Content).ToList(), cancellationToken);

        if (vectors.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count ({vectors.Count}) does not match chunk count ({chunks.Count}) for {document.SourceKey}.");
        }

        KnowledgeDocument? existing = await knowledge.FindBySourceKeyAsync(document.SourceKey, cancellationToken);
        string tags = string.Join(',', document.Tags);

        Guid documentId;
        if (existing is null)
        {
            var fresh = KnowledgeDocument.Create(
                document.SourceKey, document.Category, document.Title, document.Region,
                document.Body, tags, document.OccurredAtUtc);
            await knowledge.AddDocumentAsync(fresh, cancellationToken);
            documentId = fresh.Id;
        }
        else
        {
            existing.Replace(document.Title, document.Region, document.Body, tags, document.OccurredAtUtc);
            documentId = existing.Id;
        }

        var rows = new List<KnowledgeChunk>(chunks.Count);
        for (int i = 0; i < chunks.Count; i++)
        {
            rows.Add(KnowledgeChunk.Create(documentId, chunks[i].Ordinal, chunks[i].Content, chunks[i].TokenEstimate, vectors[i]));
        }
        await knowledge.ReplaceChunksAsync(documentId, rows, cancellationToken);

        await uow.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Indexed {SourceKey} → {Chunks} chunks ({Model})", document.SourceKey, rows.Count, embeddings.ModelName);
        return new IndexResult(1, rows.Count);
    }

    public async Task<IndexResult> IndexBatchAsync(IReadOnlyList<KnowledgeDocumentInput> documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        int docs = 0;
        int chunkCount = 0;
        foreach (KnowledgeDocumentInput d in documents)
        {
            IndexResult r = await IndexAsync(d, cancellationToken);
            docs += r.DocumentsIndexed;
            chunkCount += r.ChunksIndexed;
        }
        return new IndexResult(docs, chunkCount);
    }
}
