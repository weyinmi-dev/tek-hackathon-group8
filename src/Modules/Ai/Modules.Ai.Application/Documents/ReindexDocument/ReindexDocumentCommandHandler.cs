using Application.Abstractions.Messaging;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.ReindexDocument;

internal sealed class ReindexDocumentCommandHandler(
    IManagedDocumentRepository documents,
    IDocumentIngestionService ingestion) : ICommandHandler<ReindexDocumentCommand>
{
    public async Task<Result> Handle(ReindexDocumentCommand cmd, CancellationToken ct)
    {
        ManagedDocument? doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null)
        {
            return Result.Failure(Error.NotFound("Document.NotFound", "Document not found."));
        }

        await ingestion.IngestAsync(cmd.DocumentId, ct);
        return Result.Success();
    }
}
