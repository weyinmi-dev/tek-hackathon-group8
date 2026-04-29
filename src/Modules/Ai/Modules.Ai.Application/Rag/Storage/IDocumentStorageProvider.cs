using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Application.Rag.Storage;

/// <summary>
/// Provider-agnostic facade over a document store (local disk, Google Drive, OneDrive,
/// SharePoint, Azure Blob, ...). Each implementation owns the bytes for documents whose
/// <see cref="DocumentSource"/> matches <see cref="Source"/>; the registry resolves the
/// right provider for read/write at runtime.
/// </summary>
public interface IDocumentStorageProvider
{
    DocumentSource Source { get; }

    Task<StoredObject> SaveAsync(
        string suggestedFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}
