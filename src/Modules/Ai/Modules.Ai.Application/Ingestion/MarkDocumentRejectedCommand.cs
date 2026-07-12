using Application.Abstractions.Messaging;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Ingestion;

/// <summary>
/// Terminal reject branch of <c>DocumentIngestionWorkflow</c>: the intake agent judged the document
/// irrelevant, so it never reaches chunking. Dispatched via ISender by the workflow's reject executor.
/// </summary>
public sealed record MarkDocumentRejectedCommand(Guid DocumentId, string Reason) : ICommand;

internal sealed class MarkDocumentRejectedCommandHandler(
    IManagedDocumentRepository documents,
    IUnitOfWork uow) : ICommandHandler<MarkDocumentRejectedCommand>
{
    public async Task<Result> Handle(MarkDocumentRejectedCommand request, CancellationToken cancellationToken)
    {
        ManagedDocument? doc = await documents.GetByIdAsync(request.DocumentId, cancellationToken);
        if (doc is null)
        {
            return Result.Failure(Error.NotFound(
                "Document.NotFound", $"Document {request.DocumentId} was not found."));
        }

        doc.MarkRejected(request.Reason);
        await uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
