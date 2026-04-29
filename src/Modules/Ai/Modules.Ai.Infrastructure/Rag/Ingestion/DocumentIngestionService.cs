using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Rag.Ingestion;

/// <summary>
/// Default ingestion pipeline implementation: hand off to the storage provider,
/// extract text, then dispatch to the existing <see cref="IRagIndexer"/>.
/// Status transitions and error capture happen here so handlers stay simple.
/// </summary>
internal sealed class DocumentIngestionService(
    IManagedDocumentRepository documents,
    IDocumentStorageRegistry storage,
    IDocumentTextExtractor extractor,
    IRagIndexer indexer,
    IUnitOfWork uow,
    ILogger<DocumentIngestionService> logger) : IDocumentIngestionService
{
    public async Task<IndexResult> IngestAsync(Guid managedDocumentId, CancellationToken cancellationToken = default)
    {
        ManagedDocument doc = await documents.GetByIdAsync(managedDocumentId, cancellationToken)
            ?? throw new InvalidOperationException($"Managed document {managedDocumentId} not found.");

        doc.MarkInProgress();
        await uow.SaveChangesAsync(cancellationToken);

        try
        {
            IDocumentStorageProvider provider = storage.For(doc.Source);
            await using Stream stream = await provider.OpenReadAsync(doc.StorageKey, cancellationToken);
            string body = await extractor.ExtractAsync(stream, doc.ContentType, doc.FileName, cancellationToken);

            if (string.IsNullOrWhiteSpace(body))
            {
                doc.MarkFailed("Extractor returned empty text.");
                await uow.SaveChangesAsync(cancellationToken);
                return new IndexResult(0, 0);
            }

            // SourceKey ties the indexer's idempotency back to the managed document — re-running
            // ingestion replaces the same chunks rather than producing duplicates.
            string sourceKey = $"doc:{doc.Id}:v{doc.Version}";
            IReadOnlyList<string> tagList = string.IsNullOrWhiteSpace(doc.Tags)
                ? Array.Empty<string>()
                : doc.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var input = new KnowledgeDocumentInput(
                SourceKey: sourceKey,
                Category: doc.Category,
                Title: doc.Title,
                Region: doc.Region,
                Body: body,
                Tags: tagList,
                OccurredAtUtc: doc.UploadedAtUtc);

            IndexResult result = await indexer.IndexAsync(input, cancellationToken);

            // The indexer creates/updates the KnowledgeDocument keyed off SourceKey. We don't
            // currently expose its Id back through IRagIndexer; the link is recoverable via the
            // SourceKey convention above. Mark indexed with Empty when not surfaced — UI shows
            // the status but does not require the FK today.
            doc.MarkIndexed(Guid.Empty);
            await uow.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion failed for {DocumentId}", managedDocumentId);
            doc.MarkFailed(ex.Message);
            await uow.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
