using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.DownloadDocument;

/// <summary>
/// Was inline in the download endpoint, which meant Web.Api injected IManagedDocumentRepository and
/// handled the entity itself (Phase 1 §4.2 #4). The work is unchanged — look the document up, check
/// its provider is connected, open the file — it just lives where it belongs now, so the endpoint
/// sends a query and the failure modes come back as Errors instead of ad-hoc Problem responses.
/// </summary>
internal sealed class DownloadDocumentQueryHandler(
    IManagedDocumentRepository documents,
    IDocumentStorageRegistry storage,
    ILogger<DownloadDocumentQueryHandler> logger)
    : IQueryHandler<DownloadDocumentQuery, DocumentDownload>
{
    public async Task<Result<DocumentDownload>> Handle(DownloadDocumentQuery query, CancellationToken ct)
    {
        ManagedDocument? doc = await documents.GetByIdAsync(query.DocumentId, ct);
        if (doc is null)
        {
            return Result.Failure<DocumentDownload>(
                Error.NotFound("Document.NotFound", "Document not found."));
        }

        if (!storage.IsAvailable(doc.Source))
        {
            return Result.Failure<DocumentDownload>(Error.Problem(
                "Document.StorageUnavailable",
                $"Storage provider {doc.Source} is not connected."));
        }

        try
        {
            IDocumentStorageProvider provider = storage.For(doc.Source);
            Stream content = await provider.OpenReadAsync(doc.StorageKey, ct);

            return Result.Success(new DocumentDownload(content, doc.ContentType, doc.FileName));
        }
        catch (Exception ex)
        {
            // The row exists but the file behind it does not, or the provider is refusing us. Log the
            // detail; hand the caller a message without the provider's internals in it.
            logger.LogError(ex, "Failed to retrieve file for document {DocumentId} from {Source}",
                doc.Id, doc.Source);

            return Result.Failure<DocumentDownload>(Error.Problem(
                "Document.RetrievalFailed",
                $"Failed to retrieve file from {doc.Source}."));
        }
    }
}
