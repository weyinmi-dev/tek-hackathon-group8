using Application.Abstractions.Messaging;
using Modules.Ai.Application.Rag.Ingestion;
using SharedKernel;

namespace Modules.Ai.Application.Documents.SyncDocuments;

internal sealed class SyncDocumentsCommandHandler(IDocumentSyncService sync) : ICommandHandler<SyncDocumentsCommand>
{
    public async Task<Result> Handle(SyncDocumentsCommand cmd, CancellationToken ct)
    {
        await sync.SyncAllAsync(ct);
        return Result.Success();
    }
}
