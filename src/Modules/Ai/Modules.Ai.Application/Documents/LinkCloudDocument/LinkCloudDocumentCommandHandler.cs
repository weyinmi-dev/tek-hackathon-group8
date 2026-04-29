using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.LinkCloudDocument;

internal sealed class LinkCloudDocumentCommandHandler(
    IDocumentStorageRegistry storage,
    IManagedDocumentRepository documents,
    IDocumentIngestionService ingestion,
    IUnitOfWork uow,
    ILogger<LinkCloudDocumentCommandHandler> logger) : ICommandHandler<LinkCloudDocumentCommand, LinkedDocumentDto>
{
    public async Task<Result<LinkedDocumentDto>> Handle(LinkCloudDocumentCommand cmd, CancellationToken ct)
    {
        if (cmd.Source == DocumentSource.LocalUpload)
        {
            return Result.Failure<LinkedDocumentDto>(
                Error.Problem("Document.UseUpload", "Use the upload endpoint for local documents."));
        }

        if (!storage.IsAvailable(cmd.Source))
        {
            return Result.Failure<LinkedDocumentDto>(
                Error.Problem("Document.ProviderUnavailable",
                    $"Storage provider for {cmd.Source} is not configured. Wire the provider in Ai infrastructure DI to enable it."));
        }

        var doc = ManagedDocument.Create(
            title: cmd.Title,
            fileName: cmd.FileName,
            contentType: cmd.ContentType,
            sizeBytes: cmd.SizeBytes,
            category: cmd.Category,
            region: cmd.Region,
            tags: cmd.Tags,
            source: cmd.Source,
            storageKey: cmd.StorageKey,
            externalReference: cmd.ExternalReference,
            uploadedBy: cmd.LinkedBy);

        await documents.AddAsync(doc, ct);
        await uow.SaveChangesAsync(ct);

        try
        {
            await ingestion.IngestAsync(doc.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ingestion failed for cloud-linked document {DocumentId} ({Source})", doc.Id, cmd.Source);
        }

        ManagedDocument fresh = await documents.GetByIdAsync(doc.Id, ct) ?? doc;
        return Result.Success(new LinkedDocumentDto(fresh.Id, fresh.Title, fresh.Source, fresh.Status.ToString()));
    }
}
