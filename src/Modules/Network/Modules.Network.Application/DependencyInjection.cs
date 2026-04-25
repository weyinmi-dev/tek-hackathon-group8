using Microsoft.Extensions.DependencyInjection;

namespace Modules.Network.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNetworkApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
