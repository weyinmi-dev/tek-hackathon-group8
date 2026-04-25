using Microsoft.Extensions.DependencyInjection;

namespace Modules.Alerts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAlertsApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
