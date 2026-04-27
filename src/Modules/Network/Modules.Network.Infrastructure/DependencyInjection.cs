using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Network.Api;
using Modules.Network.Domain;
using Modules.Network.Domain.Towers;
using Modules.Network.Infrastructure.Api;
using Modules.Network.Infrastructure.Database;
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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INetworkApi, NetworkApi>();
        return services;
    }
}
