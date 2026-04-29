using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Domain.Knowledge;
using Modules.Ai.Infrastructure.Database;
using Modules.Ai.Infrastructure.Rag.Seed;
using Modules.Alerts.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Seed;
using Modules.Analytics.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Seed;
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

        _ = sp.GetRequiredService<AiDbContext>();

        RagOptions ragOptions = sp.GetRequiredService<RagOptions>();
        if (ragOptions.Enabled && ragOptions.AutoSeedCorpus)
        {
            ILogger logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("KnowledgeCorpusSeeder");
            await KnowledgeCorpusSeeder.SeedAsync(
                sp.GetRequiredService<IRagIndexer>(),
                sp.GetRequiredService<IKnowledgeRepository>(),
                logger);
        }
    }
}
