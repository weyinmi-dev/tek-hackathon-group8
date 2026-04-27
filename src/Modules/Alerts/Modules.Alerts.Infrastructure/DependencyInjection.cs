using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Alerts.Api;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using Modules.Alerts.Infrastructure.Api;
using Modules.Alerts.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Repositories;
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
        return services;
    }
}
