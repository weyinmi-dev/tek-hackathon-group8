using Microsoft.EntityFrameworkCore;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Infrastructure.Database;

namespace Modules.Ai.Infrastructure.Repositories;

internal sealed class ManagedDocumentRepository(AiDbContext db) : IManagedDocumentRepository
{
    public Task<ManagedDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ManagedDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task AddAsync(ManagedDocument document, CancellationToken cancellationToken = default) =>
        await db.ManagedDocuments.AddAsync(document, cancellationToken);

    public void Remove(ManagedDocument document) => db.ManagedDocuments.Remove(document);

    public async Task<IReadOnlyList<ManagedDocument>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.ManagedDocuments
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ManagedDocument>> ListByStatusAsync(IndexingStatus status, CancellationToken cancellationToken = default) =>
        await db.ManagedDocuments
            .AsNoTracking()
            .Where(d => d.Status == status)
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        db.ManagedDocuments.CountAsync(cancellationToken);
}
