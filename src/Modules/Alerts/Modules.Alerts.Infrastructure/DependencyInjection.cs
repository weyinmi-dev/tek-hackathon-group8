using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Alerts.Api;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using Modules.Alerts.Infrastructure.Api;
using Modules.Alerts.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Pipeline;
using Modules.Alerts.Infrastructure.Repositories;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using SharedKernel;

namespace Modules.Alerts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAlertsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("telcopilot");
        Ensure.NotNullOrEmpty(connectionString);

        services.AddDbContext<AlertsDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations_history", Schema.Alerts))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAlertsApi, AlertsApi>();

        // Stage-4 cross-module adapters: implement the ports Network.Application defined.
        services.AddScoped<IAlertSnapshotProvider, AlertSnapshotProvider>();
        services.AddScoped<IAlertActionExecutor, AlertActionExecutor>();

        return services;
    }
}
