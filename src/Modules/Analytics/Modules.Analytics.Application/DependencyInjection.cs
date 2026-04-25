using Microsoft.Extensions.DependencyInjection;

namespace Modules.Analytics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
