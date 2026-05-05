using Microsoft.Extensions.DependencyInjection;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Infrastructure.Rag.Seed;

namespace Modules.Ai.Infrastructure.Rag.Ingestion;

internal sealed class DocumentSyncService(IServiceScopeFactory scopeFactory) : IDocumentSyncService
{
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        // Fire-and-forget sync for the UI
        _ = Task.Run(async () =>
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            var localSeeder = scope.ServiceProvider.GetRequiredService<LocalDocumentSeeder>();
            await localSeeder.SeedAsync(CancellationToken.None);
        }, CancellationToken.None);
    }
}
