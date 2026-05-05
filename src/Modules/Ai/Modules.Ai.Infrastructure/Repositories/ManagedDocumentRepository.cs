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
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ManagedDocument>> ListPagedAsync(int page, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        IQueryable<ManagedDocument> query = db.ManagedDocuments;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string s = $"%{searchTerm}%";
            query = query.Where(d => EF.Functions.ILike(d.Title, s) || EF.Functions.ILike(d.FileName, s));
        }

        return await query
            .OrderByDescending(d => d.UploadedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedDocument>> ListByStatusAsync(IndexingStatus status, CancellationToken cancellationToken = default) =>
        await db.ManagedDocuments
            .Where(d => d.Status == status)
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        IQueryable<ManagedDocument> query = db.ManagedDocuments;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string s = $"%{searchTerm}%";
            query = query.Where(d => EF.Functions.ILike(d.Title, s) || EF.Functions.ILike(d.FileName, s));
        }
        return query.CountAsync(cancellationToken);
    }
}
