namespace Modules.Ai.Application.Rag.Ingestion;

public interface IDocumentSyncService
{
    Task SyncAllAsync(CancellationToken cancellationToken);
}
