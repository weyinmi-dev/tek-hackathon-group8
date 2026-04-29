using Microsoft.EntityFrameworkCore;
using Modules.Ai.Domain.Knowledge;
using Modules.Ai.Infrastructure.Database;

namespace Modules.Ai.Infrastructure.Repositories;

internal sealed class KnowledgeRepository(AiDbContext db) : IKnowledgeRepository
{
    public Task<KnowledgeDocument?> FindBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default) =>
        db.KnowledgeDocuments.FirstOrDefaultAsync(d => d.SourceKey == sourceKey, cancellationToken);

    public Task AddDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        db.KnowledgeDocuments.Add(document);
        return Task.CompletedTask;
    }

    public async Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        // Drop any prior chunks for this document, then insert the fresh batch. Both happen in
        // the unit-of-work transaction kicked off by SaveChangesAsync.
        await db.KnowledgeChunks
            .Where(c => c.DocumentId == documentId)
            .ExecuteDeleteAsync(cancellationToken);

        await db.KnowledgeChunks.AddRangeAsync(chunks, cancellationToken);
    }

    public Task<int> CountDocumentsAsync(CancellationToken cancellationToken = default) =>
        db.KnowledgeDocuments.CountAsync(cancellationToken);

    public Task<int> CountChunksAsync(CancellationToken cancellationToken = default) =>
        db.KnowledgeChunks.CountAsync(cancellationToken);
}
