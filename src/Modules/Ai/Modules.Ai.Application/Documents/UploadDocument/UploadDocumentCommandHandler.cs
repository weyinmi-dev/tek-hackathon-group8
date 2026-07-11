using Application.Abstractions.Messaging;
using global::Application.Abstractions.Events;
using Application.Abstractions.Storage;
using Modules.Ai.Application.Abstractions;
using Modules.Ai.Application.Rag.Documents;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.UploadDocument;

internal sealed class UploadDocumentCommandHandler(
    IDocumentStorageRegistry storage,
    IManagedDocumentRepository documents,
    IOutboxWriter outbox,
    IUnitOfWork uow,
    DocumentsOptions options) : ICommandHandler<UploadDocumentCommand, UploadedDocumentDto>
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

        // Enqueue the async pipeline trigger in the SAME unit of work as the document row, then
        // return immediately — ingestion (RAG indexing, and network-log analysis if Network decides
        // the file is one) runs off the outbox (Phase 3 M9). The document starts life as Pending.
        outbox.Enqueue(new DocumentUploaded(
            Id: Guid.NewGuid(),
            DocumentId: doc.Id,
            FileName: doc.FileName,
            ContentType: doc.ContentType,
            StorageKey: doc.StorageKey,
            Source: (int)doc.Source,
            SubmittedBy: doc.UploadedBy));
        await uow.SaveChangesAsync(ct);

        return Result.Success(new UploadedDocumentDto(
            doc.Id, doc.Title, doc.FileName, doc.SizeBytes, doc.Status.ToString(), doc.Source));
    }
}
