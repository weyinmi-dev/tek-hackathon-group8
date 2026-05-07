using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Documents;
using Modules.Ai.Infrastructure.Rag.Seed;

namespace Modules.Ai.Infrastructure.Rag.Seed;

/// <summary>
/// Background service that watches the local document store and additional folders for new files.
/// When a PDF is dropped in, it triggers the LocalDocumentSeeder to index it
/// without requiring a restart.
/// </summary>
public sealed class LocalDocumentWatcherService(
    DocumentsOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<LocalDocumentWatcherService> logger) : BackgroundService
{
    private readonly List<FileSystemWatcher> _watchers = [];

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var roots = new List<string> { options.LocalRoot };
        roots.AddRange(options.AdditionalWatchFolders);

        foreach (string rawRoot in roots)
        {
            string root = Path.GetFullPath(rawRoot);
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            logger.LogInformation("LocalDocumentWatcherService: monitoring {Root} for new PDFs...", root);

            var watcher = new FileSystemWatcher(root, "*.pdf")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Created += async (s, e) => await OnChangedAsync(e.FullPath, stoppingToken);
            watcher.Renamed += async (s, e) => await OnChangedAsync(e.FullPath, stoppingToken);

            _watchers.Add(watcher);
        }

        return Task.CompletedTask;
    }

    private async Task OnChangedAsync(string path, CancellationToken ct)
    {
        // Small delay to ensure the file is fully written and unlocked by the OS
        await Task.Delay(1500, ct);

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<LocalDocumentSeeder>();
            
            logger.LogInformation("LocalDocumentWatcherService: change detected in {FileName}. Triggering re-sync...", Path.GetFileName(path));
            await seeder.SeedAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LocalDocumentWatcherService: failed to process change in {Path}", path);
        }
    }

    public override void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        base.Dispose();
    }
}
