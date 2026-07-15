using Application.Abstractions.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Application.Ingestion.Stage4_Persist;

namespace Modules.Network.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNetworkApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // ── Tunable pipeline policy ─────────────────────────────────────────────
        // All three are bound from configuration. They are the numbers that decide what the system
        // calls an anomaly, and they belong to whoever runs the fleet — not to whoever wrote the rules.
        //
        // Bound and validated HERE, eagerly, not inside a DI factory. A factory is lazy: it would not
        // run until the first upload resolved the planner, so a threshold misconfigured to nonsense
        // would let the app boot clean and green and only blow up hours later, on a request, in front
        // of a user. Fail-fast means fail at startup — a bad value stops the process with a readable
        // message before anything can depend on it.
        services.AddSingleton(Bind<DecisionEngineOptions>(configuration, "Ingestion:DecisionEngine"));

        SnapshotAnomalyOptions anomalies =
            Bind<SnapshotAnomalyOptions>(configuration, SnapshotAnomalyOptions.SectionName);
        anomalies.Validate();
        services.AddSingleton(anomalies);

        SnapshotCalibrationOptions calibration =
            Bind<SnapshotCalibrationOptions>(configuration, SnapshotCalibrationOptions.SectionName);
        calibration.Validate();
        services.AddSingleton(calibration);

        // Stage 3 — pure rule-based decision engine. Stateless → singleton.
        services.AddSingleton<IDecisionEngine, DefaultDecisionEngine>();

        // Stage 3 — the snapshot counterpart. Equally pure and stateless, so equally a singleton.
        services.AddSingleton<ISiteSnapshotPlanner, SiteSnapshotPlanner>();

        // Stage 4 — applies the Network-owned half of a snapshot sync. Scoped: it holds repositories
        // bound to the request's DbContext.
        services.AddScoped<SnapshotSyncApplier>();

        return services;
    }

    /// <summary>
    /// Binds a section onto a fresh options instance, leaving any key the configuration does not
    /// mention at its default. An absent section is therefore "use the defaults", not a crash — a
    /// deployment that has never heard of these knobs still boots.
    /// </summary>
    private static T Bind<T>(IConfiguration configuration, string sectionName) where T : new()
    {
        var options = new T();
        configuration.GetSection(sectionName).Bind(options);
        return options;
    }
}
