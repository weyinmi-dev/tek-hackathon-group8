using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Application.Rag.Models;
using Application.Abstractions.Storage;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using Modules.Network.Application.Ingestion.Pipeline;

namespace Modules.Ai.Infrastructure.Rag.Ingestion;

/// <summary>
/// Default ingestion pipeline implementation: hand off to the storage provider,
/// extract text, then dispatch to the existing <see cref="IRagIndexer"/>.
/// Status transitions and error capture happen here so handlers stay simple.
///
/// After RAG indexing, files that look like network logs (csv / json / jsonl /
/// xlsx / txt / log) are also dispatched through the 5-stage network ingestion
/// pipeline so the full trigger chain fires: alerts, anomalies, optimizations,
/// topology updates, dashboard entries, and copilot KB enrichment.
/// </summary>
internal sealed class DocumentIngestionService(
    IManagedDocumentRepository documents,
    IDocumentStorageRegistry storage,
    IDocumentTextExtractor extractor,
    IDocumentValidator validator,
    IRagIndexer indexer,
    ISender sender,
    IUnitOfWork uow,
    ILogger<DocumentIngestionService> logger) : IDocumentIngestionService
{
    private static readonly HashSet<string> NetworkLogExtensions =
        new([".csv", ".json", ".jsonl", ".xlsx", ".txt", ".log"], StringComparer.OrdinalIgnoreCase);

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
                doc.MarkFailed("Extractor returned empty text — the document may be a scanned image or otherwise contain no extractable text layer.");
                await uow.SaveChangesAsync(CancellationToken.None);
                return new IndexResult(0, 0);
            }

            // AI Quality Gate: validate relevance before indexing
            string preview = body.Length > 2000 ? body[..2000] : body;
            var (isValid, reason) = await validator.ValidateAsync(doc.FileName, preview, cancellationToken);
            if (!isValid)
            {
                logger.LogWarning("Ingestion: Document {DocumentId} ({FileName}) rejected by AI: {Reason}", doc.Id, doc.FileName, reason);
                doc.MarkRejected(reason);
                await uow.SaveChangesAsync(CancellationToken.None);
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

            // For files that look like network logs, also run the full 5-stage network
            // ingestion pipeline so all downstream triggers fire: anomaly detection,
            // alert creation/update, tower metric updates, optimization proposals,
            // dashboard projection, and copilot KB enrichment. The pipeline is
            // content-hash–idempotent so re-uploading the same file is a no-op.
            await TryDispatchNetworkPipelineAsync(doc, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion failed for {DocumentId}", managedDocumentId);
            doc.MarkFailed(ex.Message);
            // Persist the failure with CancellationToken.None — if the request token was
            // cancelled (e.g. operator closed the upload modal mid-ingest), the original
            // SaveChanges would have thrown OperationCanceledException and the document
            // would be stranded at InProgress forever. The MarkFailed write itself is fast
            // enough that an unconditional save is safe here.
            await uow.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Dispatches <see cref="ProcessNetworkLogCommand"/> for documents whose file
    /// extension matches the network-log set. Runs synchronously so the demo flow
    /// is "upload → all triggers fired" without needing a background worker.
    /// Failures are non-fatal — the document is already RAG-indexed at this point.
    /// </summary>
    private async Task TryDispatchNetworkPipelineAsync(
        ManagedDocument doc,
        CancellationToken cancellationToken)
    {
        string ext = Path.GetExtension(doc.FileName);
        if (!NetworkLogExtensions.Contains(ext))
        {
            return;
        }

        try
        {
            logger.LogInformation(
                "Document {DocumentId} ({FileName}) looks like a network log — dispatching network analysis pipeline",
                doc.Id, doc.FileName);

            IDocumentStorageProvider provider = storage.For(doc.Source);
            await using Stream networkStream = await provider.OpenReadAsync(doc.StorageKey, cancellationToken);

            // The pipeline's orchestrator computes and stages the file itself, so we
            // don't need to pre-stage here. McpFilePath is left null; the handler will
            // call IFileStagingService and set it before Stage 2.
            await sender.Send(new ProcessNetworkLogCommand(
                FileName: doc.FileName,
                ContentType: doc.ContentType,
                Content: networkStream,
                SubmittedBy: doc.UploadedBy), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Network analysis pipeline skipped for document {DocumentId} — RAG index is still complete",
                doc.Id);
        }
    }
}
