using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Analytics.Api;
using Modules.Analytics.Domain;
using Modules.Analytics.Domain.Audit;
using Modules.Analytics.Infrastructure.Api;
using Modules.Analytics.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Repositories;
using SharedKernel;

namespace Modules.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");
        Ensure.NotNullOrEmpty(connectionString);

        services.AddDbContext<AnalyticsDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations_history", Schema.Analytics))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAnalyticsApi, AnalyticsApi>();
        return services;
    }
}
