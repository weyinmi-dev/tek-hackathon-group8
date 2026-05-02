using Microsoft.Extensions.DependencyInjection;

namespace Modules.Energy.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEnergyApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
