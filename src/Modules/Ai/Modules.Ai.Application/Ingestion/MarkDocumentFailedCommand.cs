using Application.Abstractions.Messaging;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Ingestion;

/// <summary>
/// Terminal failure branch of <c>DocumentIngestionWorkflow</c>: text extraction produced nothing
/// (a scanned image or a file with no extractable text layer), so there is nothing to index.
/// Distinct from Rejected — the document is valid, the pipeline just could not read it.
/// </summary>
public sealed record MarkDocumentFailedCommand(Guid DocumentId, string Error) : ICommand;

internal sealed class MarkDocumentFailedCommandHandler(
    IManagedDocumentRepository documents,
    IUnitOfWork uow) : ICommandHandler<MarkDocumentFailedCommand>
{
    public async Task<Result> Handle(MarkDocumentFailedCommand request, CancellationToken cancellationToken)
    {
        ManagedDocument? doc = await documents.GetByIdAsync(request.DocumentId, cancellationToken);
        if (doc is null)
        {
            return Result.Failure(Error.NotFound(
                "Document.NotFound", $"Document {request.DocumentId} was not found."));
        }

        doc.MarkFailed(request.Error);
        await uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
