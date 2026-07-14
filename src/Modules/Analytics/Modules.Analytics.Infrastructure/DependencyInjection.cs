using Application.Abstractions.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Analytics.Api;
using Modules.Analytics.Domain;
using Modules.Analytics.Domain.Audit;
using Modules.Analytics.Domain.Ingestion;
using Modules.Analytics.Domain.Notifications;
using Modules.Analytics.Infrastructure.Api;
using Modules.Analytics.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Notifications;
using Modules.Analytics.Infrastructure.Repositories;
using SharedKernel;

namespace Modules.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("telcopilot");
        Ensure.NotNullOrEmpty(connectionString);

        services.AddDbContext<AnalyticsDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations_history", Schema.Analytics))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IIngestionDashboardRepository, IngestionDashboardRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAnalyticsApi, AnalyticsApi>();

        // Gives the pre-existing INotificationService a real implementation. Analytics is registered
        // after AddInfrastructure in the composition root, so this replaces the log-only stub rather
        // than standing up a second notification path beside it.
        services.AddScoped<INotificationService, PersistentNotificationService>();

        return services;
    }
}
