using Microsoft.EntityFrameworkCore;
using Modules.Ai.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Database;
using Modules.Identity.Infrastructure.Database;
using Modules.Network.Infrastructure.Database;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<NetworkDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<AlertsDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<AiDbContext>().Database.EnsureCreatedAsync();
    }
}
