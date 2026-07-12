using SharedKernel;
using Microsoft.EntityFrameworkCore;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Application.Rag.Stores;
using Modules.Ai.Domain.Knowledge;
using Modules.Ai.Infrastructure.Database;
using Npgsql;
using Pgvector;

namespace Modules.Ai.Infrastructure.Rag.Stores;

/// <summary>
/// pgvector-backed implementation. Uses the <c>&lt;=&gt;</c> cosine-distance operator so the
/// ordering is stable regardless of vector magnitude, and returns <c>1 - distance</c> as
/// <c>Similarity</c> for downstream readability.
///
/// The query is raw SQL rather than the typed <c>Vector.CosineDistance</c> LINQ extension:
/// the domain <see cref="KnowledgeChunk"/> now exposes its embedding as <c>float[]</c> (no
/// pgvector dependency in the domain), so the typed extension no longer applies. The distance
/// is computed against the <c>vector</c> column directly, with the query embedding bound as a
/// pgvector parameter (the AiDbContext data source has the vector type plugin registered).
/// </summary>
internal sealed class PgVectorKnowledgeStore(AiDbContext db) : IKnowledgeStore
{
    // Category is stored as int (HasConversion<int>); region as text. Table/column names are
    // the snake_case forms produced by the naming convention, in the `ai` schema.
    private const string SearchSql =
        """
        SELECT c.id               AS "Id",
               c.document_id       AS "DocumentId",
               d.source_key        AS "SourceKey",
               d.category          AS "Category",
               d.title             AS "Title",
               d.region            AS "Region",
               c.ordinal           AS "Ordinal",
               c.content           AS "Content",
               c.embedding <=> @query AS "Distance"
        FROM ai.knowledge_chunks c
        JOIN ai.knowledge_documents d ON c.document_id = d.id
        WHERE (@category IS NULL OR d.category = @category)
          AND (@region   IS NULL OR upper(d.region) = @region)
        ORDER BY "Distance"
        LIMIT @take
        """;

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Vector queryEmbedding,
        int topK,
        KnowledgeCategory? categoryFilter,
        string? regionFilter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        int take = Math.Clamp(topK, 1, 50);

        NpgsqlParameter[] parameters =
        [
            new("query", queryEmbedding),
            new("category", categoryFilter.HasValue ? (int)categoryFilter.Value : DBNull.Value),
            new("region", string.IsNullOrWhiteSpace(regionFilter) ? DBNull.Value : regionFilter.Trim().ToUpperInvariant()),
            new("take", take),
        ];

        List<RagRow> rows = await db.Database
            .SqlQueryRaw<RagRow>(SearchSql, parameters)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new RetrievedChunk(
                r.Id, r.DocumentId, r.SourceKey, (KnowledgeCategory)r.Category, r.Title, r.Region,
                r.Ordinal, r.Content, Similarity: 1.0 - r.Distance))
            .ToList();
    }

    // Flat projection for SqlQueryRaw. Column aliases above match these property names.
    private sealed record RagRow(
        Guid Id,
        Guid DocumentId,
        string SourceKey,
        int Category,
        string Title,
        string Region,
        int Ordinal,
        string Content,
        double Distance);
}
