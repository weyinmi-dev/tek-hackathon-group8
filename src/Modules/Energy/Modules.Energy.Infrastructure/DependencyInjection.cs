using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Energy.Api;
using Modules.Energy.Domain;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;
using Modules.Energy.Infrastructure.Api;
using Modules.Energy.Infrastructure.Database;
using Modules.Energy.Infrastructure.Repositories;
using Modules.Energy.Infrastructure.Telemetry;
using SharedKernel;

namespace Modules.Energy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEnergyInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("telcopilot");
        Ensure.NotNullOrEmpty(connectionString);

        services.AddDbContext<EnergyDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations_history", Schema.Energy))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<IBatteryHealthRepository, BatteryHealthRepository>();
        services.AddScoped<ISiteEnergyLogRepository, SiteEnergyLogRepository>();
        services.AddScoped<IFuelEventRepository, FuelEventRepository>();
        services.AddScoped<IAnomalyEventRepository, AnomalyEventRepository>();
        services.AddScoped<IEnergyPredictionRepository, EnergyPredictionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEnergyApi, EnergyApi>();

        // Background mutator — only one process should own this. In a multi-replica deploy
        // it'd live behind a lease; for the single-instance modular monolith it stays here.
        services.AddHostedService<EnergyTickerService>();

        return services;
    }
}
