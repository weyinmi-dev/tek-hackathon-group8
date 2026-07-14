using Microsoft.Extensions.DependencyInjection;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Application.Ingestion.Stage4_Persist;

namespace Modules.Network.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNetworkApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Stage 3 — pure rule-based decision engine. Stateless → singleton.
        services.AddSingleton<DecisionEngineOptions>();
        services.AddSingleton<IDecisionEngine, DefaultDecisionEngine>();

        // Stage 3 — the snapshot counterpart. Equally pure and stateless, so equally a singleton.
        services.AddSingleton<ISiteSnapshotPlanner, SiteSnapshotPlanner>();

        // Stage 4 — applies the Network-owned half of a snapshot sync. Scoped: it holds repositories
        // bound to the request's DbContext.
        services.AddScoped<SnapshotSyncApplier>();

        return services;
    }
}
