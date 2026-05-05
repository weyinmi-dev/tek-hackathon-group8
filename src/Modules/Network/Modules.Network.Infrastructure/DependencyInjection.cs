using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Network.Api;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Domain;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Optimizations;
using Modules.Network.Domain.Towers;
using Modules.Network.Infrastructure.Api;
using Modules.Network.Infrastructure.Database;
using Modules.Network.Infrastructure.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using Modules.Network.Infrastructure.Repositories;
using SharedKernel;

namespace Modules.Network.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNetworkInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("telcopilot");
        Ensure.NotNullOrEmpty(connectionString);

        services.AddDbContext<NetworkDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations_history", Schema.Network))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<ITowerRepository, TowerRepository>();
        services.AddScoped<IIngestionRunRepository, IngestionRunRepository>();
        services.AddScoped<IOptimizationRepository, OptimizationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INetworkApi, NetworkApi>();

        // Stage 1 — file-format parsers. Order matters: the registry returns the first match,
        // so list more-specific parsers (xlsx, json) before catch-all ones (txt).
        services.AddSingleton<INetworkLogParser, CsvNetworkLogParser>();
        services.AddSingleton<INetworkLogParser, JsonNetworkLogParser>();
        services.AddSingleton<INetworkLogParser, XlsxNetworkLogParser>();
        services.AddSingleton<INetworkLogParser, TxtNetworkLogParser>();
        services.AddSingleton<INetworkLogParserRegistry, NetworkLogParserRegistry>();

        // Stage 3 — read port for current tower state. The matching IAlertSnapshotProvider
        // implementation lives in Alerts.Infrastructure (lands in PR 5).
        services.AddScoped<ITowerSnapshotProvider, TowerSnapshotProvider>();

        return services;
    }
}
