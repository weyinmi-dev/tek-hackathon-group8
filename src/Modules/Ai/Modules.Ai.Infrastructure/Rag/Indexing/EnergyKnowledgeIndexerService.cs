using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag;

namespace Modules.Ai.Infrastructure.Rag.Indexing;

/// <summary>
/// Periodic background indexer that re-syncs Energy state into the RAG vector store every
/// <see cref="ReindexInterval"/>. Idempotent thanks to upsert-on-SourceKey, so the cost of
/// running it on a tight cadence is bounded by the number of changed sites/anomalies.
///
/// Disabled when <see cref="RagOptions.Enabled"/> is false (no embedder available).
/// </summary>
internal sealed class EnergyKnowledgeIndexerService(
    IServiceScopeFactory scopeFactory,
    RagOptions ragOptions,
    ILogger<EnergyKnowledgeIndexerService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ReindexInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ragOptions.Enabled)
        {
            logger.LogInformation("EnergyKnowledgeIndexerService: RAG disabled — skipping.");
            return;
        }

        // Wait for the migration + seeders + initial energy ticker pass to settle so the
        // first indexer run sees a populated fleet.
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                var indexer = scope.ServiceProvider.GetRequiredService<EnergyKnowledgeIndexer>();
                await indexer.IndexAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Log + carry on. A failed indexer pass must not crash the host; the next
                // pass will retry against a clean scope.
                logger.LogError(ex, "EnergyKnowledgeIndexer pass failed; will retry next interval.");
            }

            try { await Task.Delay(ReindexInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
