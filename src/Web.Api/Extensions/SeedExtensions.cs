using Modules.Ai.Infrastructure.Database;
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

        // AI has no seed data (chat logs are accumulated at runtime), but ensure ctx is wired.
        _ = sp.GetRequiredService<AiDbContext>();
    }
}
