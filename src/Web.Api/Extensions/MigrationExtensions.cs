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

        // The AI module uses pgvector for the knowledge_chunks.embedding column. The vector
        // column type cannot be created until the extension exists. EnsureCreated does not
        // run pre-create scripts, so we issue CREATE EXTENSION IF NOT EXISTS upfront. The
        // pgvector docker image bundles the extension files; if the operator points at a
        // stock Postgres image the call will surface a clear "extension not available" error.
        await EnsurePgVectorExtensionAsync<AiDbContext>(scope);
        await EnsureSchemaAsync<AiDbContext>(scope);
    }

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

    private static async Task EnsurePgVectorExtensionAsync<TContext>(IServiceScope scope) where TContext : DbContext
    {
        TContext ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        // CREATE EXTENSION is idempotent with IF NOT EXISTS. Owns its own connection — runs
        // before EnsureCreated, which would otherwise fail trying to create a vector column.
        await ctx.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;");
    }
}
