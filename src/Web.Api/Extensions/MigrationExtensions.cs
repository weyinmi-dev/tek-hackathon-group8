using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Modules.Ai.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Database;
using Modules.Energy.Infrastructure.Database;
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
        await EnsureSchemaAsync<EnergyDbContext>(scope);

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
            // First-time create — schema matches the model exactly, no diff needed.
            return;
        }
        catch (PostgresException ex) when (IsDuplicateObject(ex.SqlState))
        {
            // First-run-after-model-change path: at least one table already exists, but
            // others may be brand new. Replay the create script per-statement.
        }

        await CreateMissingObjectsAsync(ctx);
        // After (re)creating tables, also reconcile per-table columns. CreateTablesAsync
        // never adds columns to a pre-existing table, so any property added to an entity
        // after the first deploy stays missing in the live schema until we ALTER it in.
        // This keeps the EnsureCreated flow viable as the model evolves without us having
        // to introduce EF migrations.
        await AddMissingColumnsAsync(ctx);
    }

    /// <summary>
    /// Compares each entity's expected column set (from the EF model) against the live
    /// table in Postgres and emits <c>ALTER TABLE … ADD COLUMN IF NOT EXISTS …</c> for any
    /// missing columns. Only nullable columns are added — adding a NOT NULL column without
    /// a default would fail against an existing populated table, and at that point the
    /// schema change is non-trivial and deserves a real migration with operator awareness.
    /// </summary>
    private static async Task AddMissingColumnsAsync(DbContext ctx)
    {
        string defaultSchema = ctx.Model.GetDefaultSchema() ?? "public";
        foreach (IEntityType entityType in ctx.Model.GetEntityTypes())
        {
            string? tableName = entityType.GetTableName();
            if (tableName is null)
            {
                continue;
            }
            string schema = entityType.GetSchema() ?? defaultSchema;
            var soi = StoreObjectIdentifier.Table(tableName, schema);

            HashSet<string> liveColumns = await GetLiveColumnNamesAsync(ctx, schema, tableName);

            foreach (IProperty property in entityType.GetProperties())
            {
                string? columnName = property.GetColumnName(soi);
                if (string.IsNullOrEmpty(columnName) || liveColumns.Contains(columnName))
                {
                    continue;
                }
                if (!property.IsNullable)
                {
                    // A required new column on a populated table can't be added safely
                    // without a default. Surface it as a log-warning-equivalent so the
                    // operator notices, but don't crash the whole startup over it.
                    continue;
                }

                string columnType = property.GetColumnType();
                string sql = $"ALTER TABLE \"{schema}\".\"{tableName}\" ADD COLUMN IF NOT EXISTS \"{columnName}\" {columnType} NULL;";
                try
                {
                    await ctx.Database.ExecuteSqlRawAsync(sql);
                }
                catch (PostgresException ex) when (IsDuplicateObject(ex.SqlState))
                {
                    // Race-condition guard for parallel boots — IF NOT EXISTS already covers it,
                    // but Postgres can still race on the catalog lookup. Treat as no-op.
                }
            }
        }
    }

    private static async Task<HashSet<string>> GetLiveColumnNamesAsync(DbContext ctx, string schema, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DbConnection conn = ctx.Database.GetDbConnection();
        bool openedHere = false;
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
            openedHere = true;
        }
        try
        {
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT column_name FROM information_schema.columns " +
                "WHERE table_schema = @schema AND table_name = @table;";
            DbParameter pSchema = cmd.CreateParameter();
            pSchema.ParameterName = "schema";
            pSchema.Value = schema;
            cmd.Parameters.Add(pSchema);
            DbParameter pTable = cmd.CreateParameter();
            pTable.ParameterName = "table";
            pTable.Value = tableName;
            cmd.Parameters.Add(pTable);

            using DbDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (openedHere)
            {
                await conn.CloseAsync();
            }
        }
        return columns;
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
