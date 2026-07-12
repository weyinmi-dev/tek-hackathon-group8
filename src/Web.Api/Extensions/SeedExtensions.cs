using Modules.Ai.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Seed;
using Modules.Analytics.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Seed;
using Modules.Energy.Infrastructure.Database;
using Modules.Energy.Infrastructure.Seed;
using Modules.Identity.Application.Authentication;
using Modules.Identity.Infrastructure.Database;
using Modules.Identity.Infrastructure.Seed;
using Modules.Network.Infrastructure.Database;
using Modules.Network.Infrastructure.Seed;

namespace Web.Api.Extensions;

public static class SeedExtensions
{
    public static async Task SeedDataAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        // Each module seeds itself; each is idempotent (no-op if data already present).
        await IdentitySeeder.SeedAsync(sp.GetRequiredService<IdentityDbContext>(), sp.GetRequiredService<IPasswordHasher>());
        await NetworkSeeder.SeedAsync(sp.GetRequiredService<NetworkDbContext>());
        await AlertsSeeder.SeedAsync(sp.GetRequiredService<AlertsDbContext>());
        await AnalyticsSeeder.SeedAsync(sp.GetRequiredService<AnalyticsDbContext>());
        await EnergySeeder.SeedAsync(sp.GetRequiredService<EnergyDbContext>());

        // Touch the AI DbContext so its schema is provisioned. Nothing is indexed here.
        _ = sp.GetRequiredService<AiDbContext>();

        // RAG seeding (knowledge corpus, energy → knowledge, local documents) deliberately no longer
        // runs on the boot path (Phase 3 M14; Phase 1 §4.10 #7 and #8). It used to embed the entire
        // corpus before the API could serve its first request, AND it duplicated work the hosted
        // services were already doing — the same seed ran twice every boot. KnowledgeCorpusSeederService,
        // EnergyKnowledgeIndexerService and LocalDocumentSeederService now own it, in the background.
        // All three are idempotent, and nothing serves a query before they land.
    }
}
