using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Documents;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.UploadDocument;

internal sealed class UploadDocumentCommandHandler(
    IDocumentStorageRegistry storage,
    IManagedDocumentRepository documents,
    IDocumentIngestionService ingestion,
    IUnitOfWork uow,
    DocumentsOptions options,
    ILogger<UploadDocumentCommandHandler> logger) : ICommandHandler<UploadDocumentCommand, UploadedDocumentDto>
{
    public async Task<Result<UploadedDocumentDto>> Handle(UploadDocumentCommand cmd, CancellationToken ct)
    {
        if (cmd.SizeBytes > options.MaxUploadBytes)
        {
            return Result.Failure<UploadedDocumentDto>(
                Error.Problem("Document.TooLarge", $"Upload exceeds the {options.MaxUploadBytes / (1024 * 1024)} MB limit."));
        }

        IDocumentStorageProvider provider = storage.For(DocumentSource.LocalUpload);
        StoredObject stored = await provider.SaveAsync(cmd.FileName, cmd.ContentType, cmd.Content, ct);

        var doc = ManagedDocument.Create(
            title: cmd.Title,
            fileName: cmd.FileName,
            contentType: stored.ContentType,
            sizeBytes: stored.SizeBytes,
            category: cmd.Category,
            region: cmd.Region,
            tags: cmd.Tags,
            source: DocumentSource.LocalUpload,
            storageKey: stored.StorageKey,
            externalReference: stored.ExternalReference,
            uploadedBy: cmd.UploadedBy);

        await documents.AddAsync(doc, ct);
        await uow.SaveChangesAsync(ct);

        // Synchronously ingest so the demo flow is "upload → searchable" without needing
        // a background worker. The pipeline updates the document's status itself.
        try
        {
            await ingestion.IngestAsync(doc.Id, ct);
        }
        catch (Exception ex)
        {
            // Surface the failure on the document; the upload itself stays accepted so the
            // operator can retry from the UI without re-uploading.
            logger.LogWarning(ex, "Ingestion failed for {DocumentId}", doc.Id);
        }

        ManagedDocument fresh = await documents.GetByIdAsync(doc.Id, ct) ?? doc;
        return Result.Success(new UploadedDocumentDto(
            fresh.Id, fresh.Title, fresh.FileName, fresh.SizeBytes, fresh.Status.ToString(), fresh.Source));
    }
}
