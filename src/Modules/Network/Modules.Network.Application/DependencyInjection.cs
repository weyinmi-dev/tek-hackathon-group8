using Microsoft.Extensions.DependencyInjection;
using Modules.Network.Application.Ingestion.Stage3_Decide;

namespace Modules.Network.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNetworkApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Stage 3 — pure rule-based decision engine. Stateless → singleton.
        services.AddSingleton<DecisionEngineOptions>();
        services.AddSingleton<IDecisionEngine, DefaultDecisionEngine>();

        return services;
    }
}
