using Microsoft.Extensions.DependencyInjection;

namespace Modules.Ai.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAiApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
