using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.DeleteDocument;

internal sealed class DeleteDocumentCommandHandler(
    IManagedDocumentRepository documents,
    IDocumentStorageRegistry storage,
    IUnitOfWork uow,
    ILogger<DeleteDocumentCommandHandler> logger) : ICommandHandler<DeleteDocumentCommand>
{
    public async Task<Result> Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        ManagedDocument? doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null)
        {
            return Result.Failure(Error.NotFound("Document.NotFound", "Document not found."));
        }

        // Best-effort delete from storage — keep going even if the underlying file is gone.
        try
        {
            if (storage.IsAvailable(doc.Source))
            {
                IDocumentStorageProvider provider = storage.For(doc.Source);
                await provider.DeleteAsync(doc.StorageKey, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete underlying file for {DocumentId}", doc.Id);
        }

        documents.Remove(doc);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
