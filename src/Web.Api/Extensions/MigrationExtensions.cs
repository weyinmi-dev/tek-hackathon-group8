using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Modules.Ai.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Database;
using Modules.Identity.Infrastructure.Database;
using Modules.Network.Infrastructure.Database;
using Npgsql;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        await EnsureSchemaAsync<IdentityDbContext>(scope);
        await EnsureSchemaAsync<NetworkDbContext>(scope);
        await EnsureSchemaAsync<AlertsDbContext>(scope);
        await EnsureSchemaAsync<AnalyticsDbContext>(scope);
        await EnsureSchemaAsync<AiDbContext>(scope);
    }

    // EnsureCreatedAsync short-circuits when the database already exists, so only
    // the first DbContext gets its tables created. Each module owns its own schema,
    // so we create the database once, then call CreateTablesAsync per context and
    // swallow duplicate-schema/table errors on re-runs.
    private static async Task EnsureSchemaAsync<TContext>(IServiceScope scope) where TContext : DbContext
    {
        TContext ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        var creator = (IRelationalDatabaseCreator)ctx.GetService<IDatabaseCreator>();

        if (!await creator.ExistsAsync())
        {
            await creator.CreateAsync();
        }

        try
        {
            await creator.CreateTablesAsync();
        }
        catch (PostgresException ex) when (ex.SqlState is "42P07" or "42P06")
        {
            // 42P07 = duplicate_table, 42P06 = duplicate_schema. Idempotent re-run.
        }
    }
}
