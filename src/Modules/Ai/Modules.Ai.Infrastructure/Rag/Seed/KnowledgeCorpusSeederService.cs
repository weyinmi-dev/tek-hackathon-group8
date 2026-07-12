using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Infrastructure.Rag.Seed;

/// <summary>
/// Seeds the built-in knowledge corpus in the background (Phase 3 M14).
/// </summary>
/// <remarks>
/// This used to run inline in the boot path, which meant the API could not accept traffic until it
/// had finished generating embeddings for the whole corpus (Phase 1 §4.10 #7). Seeding is
/// idempotent and nothing serves a query before it lands, so it belongs off the startup thread —
/// the sibling energy/local-document seeders already work this way.
/// </remarks>
public sealed class KnowledgeCorpusSeederService(
    IServiceScopeFactory scopeFactory,
    ILogger<KnowledgeCorpusSeederService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            RagOptions options = sp.GetRequiredService<RagOptions>();
            if (!options.Enabled || !options.AutoSeedCorpus)
            {
                return;
            }

            await KnowledgeCorpusSeeder.SeedAsync(
                sp.GetRequiredService<IRagIndexer>(),
                sp.GetRequiredService<IKnowledgeRepository>(),
                logger);
        }
        catch (OperationCanceledException)
        {
            // Shutdown during seeding — nothing to report.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KnowledgeCorpusSeederService: corpus seed failed.");
        }
    }
}
