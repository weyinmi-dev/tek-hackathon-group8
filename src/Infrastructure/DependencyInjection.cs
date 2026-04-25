using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Events;
using Application.Abstractions.Notifications;
using Infrastructure.Caching;
using Infrastructure.Database;
using Infrastructure.Events;
using Infrastructure.Notifications;
using Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<INotificationService, NotificationService>();

        AddDatabase(services, configuration);
        AddCaching(services, configuration);
        AddHealthChecks(services, configuration);
        AddMessaging(services);

        return services;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");
        Ensure.NotNullOrEmpty(connectionString);

        services.AddSingleton<IDbConnectionFactory>(_ =>
            new DbConnectionFactory(new NpgsqlDataSourceBuilder(connectionString).Build()));
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        string? redisConnectionString = configuration.GetConnectionString("Cache");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICacheService, CacheService>();
    }

    private static void AddHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        IHealthChecksBuilder hc = services.AddHealthChecks();

        string? db = configuration.GetConnectionString("Database");
        if (!string.IsNullOrWhiteSpace(db))
        {
            hc.AddNpgSql(db);
        }

        string? cache = configuration.GetConnectionString("Cache");
        if (!string.IsNullOrWhiteSpace(cache))
        {
            hc.AddRedis(cache);
        }
    }

    private static void AddMessaging(IServiceCollection services)
    {
        services.AddSingleton<InMemoryMessageQueue>();
        services.AddTransient<IEventBus, EventBus>();
        services.AddHostedService<IntegrationEventProcessorJob>();
    }
}
