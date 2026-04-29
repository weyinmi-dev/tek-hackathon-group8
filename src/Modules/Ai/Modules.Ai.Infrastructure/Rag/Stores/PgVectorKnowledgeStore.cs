using Microsoft.EntityFrameworkCore;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Application.Rag.Stores;
using Modules.Ai.Domain.Knowledge;
using Modules.Ai.Infrastructure.Database;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Modules.Ai.Infrastructure.Rag.Stores;

/// <summary>
/// pgvector-backed implementation. Uses the <c>&lt;=&gt;</c> cosine distance
/// operator (exposed by <see cref="VectorExtensions.CosineDistance"/>) so the
/// ordering is stable regardless of vector magnitude. Returns
/// 1 - distance as <c>Similarity</c> for downstream readability.
/// </summary>
internal sealed class PgVectorKnowledgeStore(AiDbContext db) : IKnowledgeStore
{
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Vector queryEmbedding,
        int topK,
        KnowledgeCategory? categoryFilter,
        string? regionFilter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        int take = Math.Clamp(topK, 1, 50);

        IQueryable<KnowledgeChunk> chunks = db.KnowledgeChunks.AsNoTracking();

        // Filter by joining to the parent document. EF translates this into a single SQL JOIN.
        IQueryable<KnowledgeDocument> docs = db.KnowledgeDocuments.AsNoTracking();
        if (categoryFilter is not null)
        {
            docs = docs.Where(d => d.Category == categoryFilter.Value);
        }
        if (!string.IsNullOrWhiteSpace(regionFilter))
        {
            docs = docs.Where(d => d.Region == regionFilter);
        }

        var rows = await chunks
            .Join(docs, c => c.DocumentId, d => d.Id, (c, d) => new
            {
                c.Id,
                c.DocumentId,
                d.SourceKey,
                d.Category,
                d.Title,
                d.Region,
                c.Ordinal,
                c.Content,
                Distance = c.Embedding.CosineDistance(queryEmbedding),
            })
            .OrderBy(r => r.Distance)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new RetrievedChunk(
                r.Id, r.DocumentId, r.SourceKey, r.Category, r.Title, r.Region,
                r.Ordinal, r.Content, Similarity: 1.0 - r.Distance))
            .ToList();
    }
}
