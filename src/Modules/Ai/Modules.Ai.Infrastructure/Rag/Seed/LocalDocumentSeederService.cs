using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Modules.Ai.Infrastructure.Rag.Seed;

/// <summary>
/// Hosted background service that runs one seed pass at startup so any PDFs
/// already in the local document store are indexed before the first query.
/// </summary>
public sealed class LocalDocumentSeederService(
    IServiceScopeFactory scopeFactory,
    ILogger<LocalDocumentSeederService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<LocalDocumentSeeder>();
            await seeder.SeedAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LocalDocumentSeederService: startup seed failed");
        }
    }
}
