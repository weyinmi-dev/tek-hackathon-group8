namespace Modules.Ai.Domain.Knowledge;

public interface IKnowledgeRepository
{
    Task<KnowledgeDocument?> FindBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default);
    Task AddDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);
    Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);
    Task<int> CountDocumentsAsync(CancellationToken cancellationToken = default);
    Task<int> CountChunksAsync(CancellationToken cancellationToken = default);
}
