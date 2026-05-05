namespace Modules.Ai.Domain.Documents;

public interface IManagedDocumentRepository
{
    Task<ManagedDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ManagedDocument document, CancellationToken cancellationToken = default);
    void Remove(ManagedDocument document);
    Task<IReadOnlyList<ManagedDocument>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManagedDocument>> ListPagedAsync(int page, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManagedDocument>> ListByStatusAsync(IndexingStatus status, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? searchTerm = null, CancellationToken cancellationToken = default);
}
