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
        // column type cannot be created until the extension exists. We create the extension
        // via a raw NpgsqlConnection (NOT through the AI module's NpgsqlDataSource) because
        // that data source registers UseVector() — which loads the vector type's OID at the
        // first connection. If the extension does not yet exist when the data source first
        // opens, the type mapping is never registered for the lifetime of the data source,
        // and any subsequent attempt to write a Pgvector.Vector parameter throws
        // "no NpgsqlDbType". Bootstrapping the extension on a separate connection sidesteps
        // that.
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        string? connectionString = configuration.GetConnectionString("telcopilot");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            await EnsurePgVectorExtensionAsync(connectionString);
        }
        await EnsureSchemaAsync<AiDbContext>(scope);
    }

    /// <summary>
    /// Idempotently creates the database + every table in the model. EF's
    /// <see cref="IRelationalDatabaseCreator.CreateTablesAsync"/> runs all DDL in one batch
    /// and aborts on the first duplicate, which means new tables added to the model after
    /// the first run never get created. To survive that we fall back to executing the
    /// generated create script statement-by-statement, swallowing duplicate-table /
    /// duplicate-schema / duplicate-index errors per statement.
    /// </summary>
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
            return;
        }
        catch (PostgresException ex) when (IsDuplicateObject(ex.SqlState))
        {
            // First-run-after-model-change path: at least one table already exists, but
            // others may be brand new. Replay the create script per-statement.
        }

        await CreateMissingObjectsAsync(ctx);
    }

    private static async Task CreateMissingObjectsAsync(DbContext ctx)
    {
        string script = ctx.Database.GenerateCreateScript();
        foreach (string statement in SplitPostgresStatements(script))
        {
            string trimmed = statement.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            try
            {
                await ctx.Database.ExecuteSqlRawAsync(trimmed);
            }
            catch (PostgresException ex) when (IsDuplicateObject(ex.SqlState))
            {
                // 42P07 duplicate_table, 42P06 duplicate_schema, 42710 duplicate_object
                // (covers indexes, constraints). Idempotent re-run.
            }
        }
    }

    /// <summary>
    /// Splits a Postgres DDL script on top-level semicolons. Honours dollar-quoted
    /// strings (<c>$tag$ ... $tag$</c>), single-quoted literals (<c>'...'</c> with
    /// SQL-escape doubling), and line / block comments — EF emits <c>DO $EF$ ... END $EF$;</c>
    /// blocks for IF-NOT-EXISTS schema creation, and a naive split on <c>;</c> would
    /// chop those in half and produce <em>unterminated dollar-quoted string</em>.
    /// </summary>
    internal static IEnumerable<string> SplitPostgresStatements(string script)
    {
        int start = 0;
        int i = 0;
        string? dollarTag = null;
        bool inSingleQuote = false;
        bool inLineComment = false;
        bool inBlockComment = false;

        while (i < script.Length)
        {
            char c = script[i];

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                }
                i++;
                continue;
            }
            if (inBlockComment)
            {
                if (c == '*' && i + 1 < script.Length && script[i + 1] == '/')
                {
                    inBlockComment = false;
                    i += 2;
                    continue;
                }
                i++;
                continue;
            }

            if (dollarTag is not null)
            {
                if (c == '$' && script.AsSpan(i).StartsWith(dollarTag))
                {
                    i += dollarTag.Length;
                    dollarTag = null;
                    continue;
                }
                i++;
                continue;
            }

            if (inSingleQuote)
            {
                if (c == '\'')
                {
                    // '' inside a string is an escaped quote, not the closing one.
                    if (i + 1 < script.Length && script[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }
                    inSingleQuote = false;
                }
                i++;
                continue;
            }

            // Detect comment / quote / dollar-tag openings outside any string.
            if (c == '-' && i + 1 < script.Length && script[i + 1] == '-')
            {
                inLineComment = true;
                i += 2;
                continue;
            }
            if (c == '/' && i + 1 < script.Length && script[i + 1] == '*')
            {
                inBlockComment = true;
                i += 2;
                continue;
            }
            if (c == '\'')
            {
                inSingleQuote = true;
                i++;
                continue;
            }
            if (c == '$')
            {
                int close = script.IndexOf('$', i + 1);
                if (close > i)
                {
                    string tag = script.Substring(i + 1, close - i - 1);
                    if (IsValidDollarTag(tag))
                    {
                        dollarTag = string.Concat("$", tag, "$");
                        i = close + 1;
                        continue;
                    }
                }
            }

            if (c == ';')
            {
                yield return script[start..i];
                start = i + 1;
            }

            i++;
        }

        if (start < script.Length)
        {
            yield return script[start..];
        }
    }

    private static bool IsValidDollarTag(string tag)
    {
        if (tag.Length == 0)
        {
            return true;
        }
        char first = tag[0];
        if (!char.IsLetter(first) && first != '_')
        {
            return false;
        }
        for (int i = 1; i < tag.Length; i++)
        {
            char ch = tag[i];
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsDuplicateObject(string? sqlState) =>
        sqlState is "42P07" or "42P06" or "42710";

    private static async Task EnsurePgVectorExtensionAsync(string connectionString)
    {
        // CREATE EXTENSION is idempotent with IF NOT EXISTS. Owns its own raw NpgsqlConnection
        // so the AI module's vector-aware NpgsqlDataSource hasn't initialized yet — the data
        // source loads the `vector` type's OID at its first connection, so the extension must
        // already exist by then.
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
